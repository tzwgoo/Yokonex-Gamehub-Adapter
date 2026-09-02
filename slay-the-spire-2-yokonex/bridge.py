from __future__ import annotations

import json
import os
import re
import sys
import time
import uuid
from collections import Counter
from pathlib import Path
from typing import Any

try:
    from yokonex_event_client import GatewayClient, build_event
except ModuleNotFoundError:
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "_shared"))
    from yokonex_event_client import GatewayClient, build_event


SOURCE = "slay_the_spire_2"
MOD_EVENT_TYPES = (
    "player.damaged",
    "player.healed",
    "player.energy_changed",
    "player.block_broken",
    "player.died",
    "run.abandoned",
    "orb.lightning.passive_triggered",
    "orb.lightning.evoked",
    "orb.frost.passive_triggered",
    "orb.frost.evoked",
    "orb.dark.passive_triggered",
    "orb.dark.evoked",
    "orb.plasma.passive_triggered",
    "orb.plasma.evoked",
    "orb.glass.passive_triggered",
    "orb.glass.evoked",
    "item.purchased",
    "card.upgraded",
    "card.removed",
    "reward.selected",
    "event.encountered",
)


def mod_event_key(event_type: str) -> str:
    return f"{SOURCE}.{event_type.replace('.', '_')}"


COMMANDS = {
    key: key.replace(".", "-").replace("_", "-")
    for key in (
        "slay_the_spire_2.run_started",
        "slay_the_spire_2.run_ended",
        "slay_the_spire_2.floor_entered",
        "slay_the_spire_2.combat_started",
        "slay_the_spire_2.combat_won",
        "slay_the_spire_2.combat_lost",
        "slay_the_spire_2.player_damaged",
        "slay_the_spire_2.player_healed",
        "slay_the_spire_2.card_played",
        "slay_the_spire_2.card_gained",
        "slay_the_spire_2.relic_gained",
        "slay_the_spire_2.potion_gained",
        "slay_the_spire_2.potion_used",
        "slay_the_spire_2.gold_gained",
        *(mod_event_key(event_type) for event_type in MOD_EVENT_TYPES),
    )
}
PATTERNS = (
    (re.compile(r"Local player (\d+) is ready"), "run_started"),
    (re.compile(r"Moving to coordinate MapCoord \((\d+), (\d+)\)"), "floor_entered"),
    (re.compile(r"Creating NCombatRoom with mode=ActiveCombat encounter=(\w+)"), "combat_started"),
    (re.compile(r"(CHARACTER\.\w+) has won against encounter (ENCOUNTER\.\w+)"), "combat_won"),
    (re.compile(r"(CHARACTER\.\w+) fought (ENCOUNTER\.\w+) for the first time and LOST"), "combat_lost"),
    (re.compile(r"Player \d+ playing card (\w+)"), "card_played"),
    (re.compile(r"Obtained (CARD\.\w+) from card reward"), "card_gained"),
    (re.compile(r"Obtained (POTION\.\w+) from potion reward"), "potion_gained"),
    (re.compile(r"Player \d+ using potion (\w+)"), "potion_used"),
    (re.compile(r"Obtained (\d+) gold from reward"), "gold_gained"),
    (re.compile(r"Saved run history: (\d+)\.run"), "run_ended"),
)


def app_data_root() -> Path:
    configured = os.environ.get("STS2_DATA_ROOT", "").strip()
    if configured:
        return Path(configured).expanduser()
    return Path(os.environ.get("APPDATA", Path.home() / "AppData/Roaming")) / "SlayTheSpire2"


def normalize_mod_event(message: dict[str, Any]) -> tuple[str, dict[str, Any]] | None:
    if message.get("kind") != "event" or message.get("type") not in MOD_EVENT_TYPES:
        return None
    envelope = message.get("data")
    if not isinstance(envelope, dict):
        return None
    # 当前独立 Mod 由 C# 默认序列化器写入 PascalCase，兼容后续显式 camelCase 格式。
    payload = envelope.get("payload", envelope.get("Payload"))
    data = dict(payload) if isinstance(payload, dict) else {}
    # 保留原始上下文，便于 GameHub 做楼层、房间和跑局规则。
    for source_names, target_name in (
        (("eventId", "EventId"), "upstreamEventId"),
        (("runId", "RunId"), "runId"),
        (("floor", "Floor"), "floor"),
        (("roomType", "RoomType"), "roomType"),
    ):
        value = next((envelope[name] for name in source_names if envelope.get(name) is not None), None)
        if value is not None:
            data[target_name] = value
    return mod_event_key(message["type"]), data


def parse_mod_event_line(line: str) -> tuple[str, dict[str, Any]] | None:
    try:
        message = json.loads(line)
    except json.JSONDecodeError:
        return None
    return normalize_mod_event(message) if isinstance(message, dict) else None


def parse_log_line(line: str) -> tuple[str, dict[str, Any]] | None:
    for pattern, name in PATTERNS:
        match = pattern.search(line)
        if not match:
            continue
        values = match.groups()
        if name == "floor_entered":
            data = {"act": int(values[0]) + 1, "floorInAct": int(values[1]) + 1}
        elif name in ("combat_won", "combat_lost"):
            data = {"character": values[0], "encounter": values[1]}
        elif name == "combat_started":
            data = {"encounter": values[0]}
        elif name in ("card_played", "card_gained", "potion_gained", "potion_used"):
            data = {"id": values[0]}
        elif name == "gold_gained":
            data = {"amount": int(values[0])}
        elif name == "run_started":
            data = {"playerIndex": int(values[0])}
        else:
            data = {"runId": values[0]}
        return f"{SOURCE}.{name}", data
    if "Disconnected. Reason: QuitGameOver" in line:
        return f"{SOURCE}.run_ended", {"reason": "QuitGameOver"}
    return None


def find_current_save() -> Path | None:
    candidates = list((app_data_root() / "steam").glob("*/**/saves/current_run*.save"))
    existing = [path for path in candidates if path.is_file()]
    return max(existing, key=lambda path: path.stat().st_mtime) if existing else None


def read_save(path: Path | None) -> dict[str, Any] | None:
    if not path:
        return None
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    players = data.get("players") or []
    if not players:
        return None
    steam_id = next((part for part in path.parts if part.isdigit() and len(part) >= 16), "")
    player = next((item for item in players if str(item.get("id", "")) == steam_id), players[0])
    floor = sum(len(act) for act in data.get("map_point_history") or [])
    return {
        "startTime": data.get("start_time"),
        "character": player.get("character_id") or player.get("character"),
        "health": player.get("current_hp", 0),
        "maxHealth": player.get("max_hp", 0),
        "floor": floor,
        "relics": [item.get("id") for item in player.get("relics") or [] if item.get("id")],
    }


def save_events(
    previous: dict[str, Any] | None, current: dict[str, Any] | None
) -> list[tuple[str, dict[str, Any]]]:
    if current is None:
        return []
    if previous is None:
        return [
            (
                f"{SOURCE}.run_started",
                {"character": current["character"], "startTime": current["startTime"]},
            )
        ]
    if current["startTime"] != previous["startTime"]:
        return [
            (
                f"{SOURCE}.run_started",
                {"character": current["character"], "startTime": current["startTime"]},
            )
        ]
    events: list[tuple[str, dict[str, Any]]] = []
    # 存档兜底事件也带上最大血量，保证动态强度计算不依赖独立 Mod。
    old_hp, new_hp = previous["health"], current["health"]
    if new_hp < old_hp:
        events.append((f"{SOURCE}.player_damaged", {
            "amount": old_hp - new_hp,
            "health": new_hp,
            "maxHealth": current["maxHealth"],
        }))
    elif new_hp > old_hp:
        events.append((f"{SOURCE}.player_healed", {
            "amount": new_hp - old_hp,
            "health": new_hp,
            "maxHealth": current["maxHealth"],
        }))
    for relic in (Counter(current["relics"]) - Counter(previous["relics"])).elements():
        events.append((f"{SOURCE}.relic_gained", {"id": relic}))
    return events


def main() -> None:
    gateway = GatewayClient()
    session_id = f"slay-the-spire-2-{uuid.uuid4()}"
    log_path = app_data_root() / "logs" / "godot.log"
    mod_event_path = app_data_root() / "yokonex_events.log"
    log_stream = None
    mod_event_stream = None
    previous_save: dict[str, Any] | None = None
    run_active: bool | None = None
    seen_mod_event_ids: set[str] = set()
    seen_mod_event_order: list[str] = []
    last_mod_event_at = 0.0
    last_save_check = 0.0
    while True:
        gateway.maintain()
        if mod_event_stream is None and mod_event_path.is_file():
            mod_event_stream = mod_event_path.open("r", encoding="utf-8", errors="replace")
            mod_event_stream.seek(0, os.SEEK_END)
        if mod_event_stream:
            # 一次排空一批事件，避免连续球体触发造成积压。
            for _ in range(100):
                line = mod_event_stream.readline()
                if not line:
                    break
                parsed = parse_mod_event_line(line)
                if not parsed:
                    continue
                key, data = parsed
                upstream_id = str(data.get("upstreamEventId") or "")
                if upstream_id and upstream_id in seen_mod_event_ids:
                    continue
                if upstream_id:
                    seen_mod_event_ids.add(upstream_id)
                    seen_mod_event_order.append(upstream_id)
                    if len(seen_mod_event_order) > 500:
                        seen_mod_event_ids.discard(seen_mod_event_order.pop(0))
                last_mod_event_at = time.monotonic()
                gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
            if mod_event_path.stat().st_size < mod_event_stream.tell():
                mod_event_stream.close()
                mod_event_stream = None
        if log_stream is None and log_path.is_file():
            log_stream = log_path.open("r", encoding="utf-8", errors="replace")
            log_stream.seek(0, os.SEEK_END)
        if log_stream:
            line = log_stream.readline()
            if line:
                parsed = parse_log_line(line)
                if parsed:
                    key, data = parsed
                    if key.endswith(".run_started"):
                        if run_active is not True:
                            gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
                        run_active = True
                    elif key.endswith(".run_ended"):
                        if run_active is not False:
                            gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
                        run_active = False
                        previous_save = None
                    else:
                        gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
            elif log_path.stat().st_size < log_stream.tell():
                log_stream.close()
                log_stream = None
        if time.monotonic() - last_save_check >= 0.5:
            current_save = read_save(find_current_save())
            for key, data in save_events(previous_save, current_save):
                if (
                    time.monotonic() - last_mod_event_at < 5
                    and key.endswith((".player_damaged", ".player_healed"))
                ):
                    continue
                # 日志和存档都可能报告开局；只保留第一次，但新存档必须重新上报。
                if key.endswith(".run_started"):
                    changed_run = (
                        previous_save is not None
                        and current_save is not None
                        and previous_save["startTime"] != current_save["startTime"]
                    )
                    if run_active is True and not changed_run:
                        continue
                    run_active = True
                gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
            previous_save = current_save
            last_save_check = time.monotonic()
        time.sleep(0.05)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
