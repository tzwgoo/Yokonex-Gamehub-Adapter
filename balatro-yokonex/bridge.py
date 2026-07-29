from __future__ import annotations

import json
import os
import sys
import time
import uuid
from pathlib import Path
from typing import Any

try:
    from yokonex_event_client import GatewayClient, build_event
except ModuleNotFoundError:
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "_shared"))
    from yokonex_event_client import GatewayClient, build_event


SOURCE = "balatro"
COMMANDS = {
    "balatro.run_started": "balatro-run-started",
    "balatro.hand_played": "balatro-hand-played",
    "balatro.round_ended": "balatro-round-ended",
    "balatro.boss_blind_defeated": "balatro-boss-blind-defeated",
    "balatro.joker_gained": "balatro-joker-gained",
    "balatro.joker_lost": "balatro-joker-lost",
    "balatro.consumable_gained": "balatro-consumable-gained",
    "balatro.money_gained": "balatro-money-gained",
    "balatro.money_spent": "balatro-money-spent",
    "balatro.game_over": "balatro-game-over",
}


def event_log_path() -> Path:
    configured = os.environ.get("BALATRO_EVENT_LOG", "").strip()
    if configured:
        return Path(configured).expanduser()
    return Path(os.environ.get("APPDATA", Path.home() / "AppData/Roaming")) / "Balatro" / "yokonex_events.log"


def parse_line(line: str) -> tuple[str, dict[str, Any]] | None:
    try:
        message = json.loads(line)
    except json.JSONDecodeError:
        return None
    event_key = str(message.get("eventKey") or "")
    data = message.get("data")
    if event_key not in COMMANDS or not isinstance(data, dict):
        return None
    return event_key, data


def follow(path: Path):
    with path.open("r", encoding="utf-8", errors="replace") as stream:
        # 启动时跳过历史牌局，避免重复触发设备动作。
        stream.seek(0, os.SEEK_END)
        while True:
            line = stream.readline()
            if line:
                yield line
            else:
                if path.stat().st_size < stream.tell():
                    return
                yield None


def main() -> None:
    gateway = GatewayClient()
    session_id = f"balatro-{uuid.uuid4()}"
    while True:
        path = event_log_path()
        if not path.is_file():
            gateway.maintain()
            time.sleep(1)
            continue
        for line in follow(path):
            gateway.maintain()
            if line:
                parsed = parse_line(line)
                if parsed:
                    key, data = parsed
                    gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
            time.sleep(0.1)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
