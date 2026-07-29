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


SOURCE = "cult_of_the_lamb"
COMMANDS = {
    key: key.replace(".", "-").replace("_", "-")
    for key in (
        "cult_of_the_lamb.crusade_started",
        "cult_of_the_lamb.crusade_ended",
        "cult_of_the_lamb.player_damaged",
        "cult_of_the_lamb.player_healed",
        "cult_of_the_lamb.player_died",
        "cult_of_the_lamb.enemy_killed",
        "cult_of_the_lamb.follower_joined",
        "cult_of_the_lamb.follower_died",
        "cult_of_the_lamb.follower_revived",
        "cult_of_the_lamb.tarot_gained",
        "cult_of_the_lamb.tarot_lost",
    )
}


def log_candidates() -> list[Path]:
    configured = os.environ.get("COTL_EVENT_LOG", "").strip()
    if configured:
        return [Path(configured).expanduser()]
    roots = [
        Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)"))
        / "Steam/steamapps/common/Cult of the Lamb",
        Path(os.environ.get("ProgramFiles", "C:/Program Files"))
        / "Steam/steamapps/common/Cult of the Lamb",
    ]
    return [root / "BepInEx/yokonex_events.log" for root in roots]


def find_log() -> Path | None:
    paths = [path for path in log_candidates() if path.is_file()]
    return max(paths, key=lambda path: path.stat().st_mtime) if paths else None


def parse_line(line: str) -> tuple[str, dict[str, Any]] | None:
    try:
        message = json.loads(line)
    except json.JSONDecodeError:
        return None
    key = str(message.get("eventKey") or "")
    data = message.get("data")
    if key not in COMMANDS or not isinstance(data, dict):
        return None
    return key, data


def main() -> None:
    gateway = GatewayClient()
    session_id = f"cult-of-the-lamb-{uuid.uuid4()}"
    stream = None
    path: Path | None = None
    while True:
        gateway.maintain()
        if stream is None:
            path = find_log()
            if path:
                stream = path.open("r", encoding="utf-8", errors="replace")
                stream.seek(0, os.SEEK_END)
            else:
                time.sleep(1)
                continue
        line = stream.readline()
        if line:
            parsed = parse_line(line)
            if parsed:
                key, data = parsed
                gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
        elif path and path.stat().st_size < stream.tell():
            stream.close()
            stream = None
        time.sleep(0.1)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
