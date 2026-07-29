from __future__ import annotations

import csv
import os
import re
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


SOURCE = "wow"
COMMANDS = {
    "wow.encounter_started": "wow-encounter-started",
    "wow.encounter_won": "wow-encounter-won",
    "wow.encounter_lost": "wow-encounter-lost",
    "wow.player_damaged": "wow-player-damaged",
    "wow.player_healed": "wow-player-healed",
    "wow.player_died": "wow-player-died",
    "wow.enemy_killed": "wow-enemy-killed",
    "wow.spell_cast": "wow-spell-cast",
    "wow.spell_interrupted": "wow-spell-interrupted",
}

LOG_PREFIX = re.compile(r"^\s*\d+/\d+\s+\d+:\d+:\d+(?:\.\d+)?\s{2,}(?P<payload>.+)$")
AFFILIATION_MINE = 0x00000001
TYPE_PLAYER = 0x00000400


def _registry_install_paths() -> list[Path]:
    if os.name != "nt":
        return []
    try:
        import winreg
    except ImportError:
        return []
    paths: list[Path] = []
    keys = (
        (winreg.HKEY_CURRENT_USER, r"SOFTWARE\Blizzard Entertainment\World of Warcraft"),
        (winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Blizzard Entertainment\World of Warcraft"),
    )
    for hive, name in keys:
        try:
            with winreg.OpenKey(hive, name) as key:
                value, _ = winreg.QueryValueEx(key, "InstallPath")
                paths.append(Path(value))
        except OSError:
            continue
    return paths


def log_candidates() -> list[Path]:
    configured = os.environ.get("WOW_COMBAT_LOG", "").strip()
    if configured:
        return [Path(configured).expanduser()]
    roots = [
        *_registry_install_paths(),
        Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "World of Warcraft",
        Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "World of Warcraft",
    ]
    variants = ("_retail_", "_classic_", "_classic_era_", "_anniversary_")
    candidates: list[Path] = []
    for root in roots:
        candidates.append(root / "Logs/WoWCombatLog.txt")
        candidates.extend(root / variant / "Logs/WoWCombatLog.txt" for variant in variants)
    return candidates


def find_log() -> Path | None:
    existing = [path for path in log_candidates() if path.is_file()]
    return max(existing, key=lambda path: path.stat().st_mtime) if existing else None


def _integer(value: str) -> int:
    try:
        return int(value, 0)
    except (TypeError, ValueError):
        return 0


def _is_local_player(flags: str) -> bool:
    value = _integer(flags)
    return bool(value & AFFILIATION_MINE and value & TYPE_PLAYER)


def parse_line(line: str) -> tuple[str, dict[str, Any]] | None:
    match = LOG_PREFIX.match(line)
    if not match:
        return None
    try:
        fields = next(csv.reader([match.group("payload")], skipinitialspace=True))
    except csv.Error:
        return None
    if not fields:
        return None

    event = fields[0]
    if event == "ENCOUNTER_START" and len(fields) >= 5:
        return "wow.encounter_started", {
            "encounterId": _integer(fields[1]),
            "name": fields[2],
            "difficultyId": _integer(fields[3]),
            "groupSize": _integer(fields[4]),
        }
    if event == "ENCOUNTER_END" and len(fields) >= 6:
        key = "wow.encounter_won" if _integer(fields[5]) == 1 else "wow.encounter_lost"
        return key, {
            "encounterId": _integer(fields[1]),
            "name": fields[2],
            "difficultyId": _integer(fields[3]),
            "groupSize": _integer(fields[4]),
        }
    if len(fields) < 9:
        return None

    source_name, source_flags = fields[2], fields[3]
    target_name, target_flags = fields[6], fields[7]
    source_is_player = _is_local_player(source_flags)
    target_is_player = _is_local_player(target_flags)

    if event in {"SWING_DAMAGE", "RANGE_DAMAGE", "SPELL_DAMAGE", "SPELL_PERIODIC_DAMAGE", "ENVIRONMENTAL_DAMAGE"}:
        if not target_is_player:
            return None
        amount_index = 9 if event == "SWING_DAMAGE" else 12
        if event == "ENVIRONMENTAL_DAMAGE":
            amount_index = 10
        if len(fields) <= amount_index:
            return None
        data: dict[str, Any] = {"amount": _integer(fields[amount_index]), "source": source_name}
        if event not in {"SWING_DAMAGE", "ENVIRONMENTAL_DAMAGE"} and len(fields) > 10:
            data.update(spellId=_integer(fields[9]), spell=fields[10])
        return "wow.player_damaged", data

    if event in {"SPELL_HEAL", "SPELL_PERIODIC_HEAL"} and target_is_player and len(fields) > 13:
        return "wow.player_healed", {
            "amount": _integer(fields[12]),
            "overhealing": _integer(fields[13]),
            "spellId": _integer(fields[9]),
            "spell": fields[10],
            "source": source_name,
        }
    if event == "UNIT_DIED" and target_is_player:
        return "wow.player_died", {"player": target_name}
    if event == "PARTY_KILL" and source_is_player:
        return "wow.enemy_killed", {"target": target_name}
    if event == "SPELL_CAST_SUCCESS" and source_is_player and len(fields) > 10:
        return "wow.spell_cast", {
            "spellId": _integer(fields[9]),
            "spell": fields[10],
            "target": target_name,
        }
    if event == "SPELL_INTERRUPT" and source_is_player and len(fields) > 13:
        return "wow.spell_interrupted", {
            "spellId": _integer(fields[9]),
            "spell": fields[10],
            "interruptedSpellId": _integer(fields[12]),
            "interruptedSpell": fields[13],
            "target": target_name,
        }
    return None


def main() -> None:
    gateway = GatewayClient()
    session_id = f"wow-{uuid.uuid4()}"
    stream = None
    path: Path | None = None
    while True:
        gateway.maintain()
        latest = find_log()
        if stream is None and latest:
            path = latest
            stream = path.open("r", encoding="utf-8", errors="replace")
            # 不重放旧战斗，避免启动 GameHub 时突然触发历史指令。
            stream.seek(0, os.SEEK_END)
        if stream is None:
            time.sleep(1)
            continue
        line = stream.readline()
        if line:
            parsed = parse_line(line)
            if parsed:
                key, data = parsed
                gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
        elif latest != path or (path and path.stat().st_size < stream.tell()):
            stream.close()
            stream = None
        time.sleep(0.1)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
