from __future__ import annotations

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


SOURCE = "hearthstone"
COMMANDS = {
    "hearthstone.match_started": "hearthstone-match-started",
    "hearthstone.turn_started": "hearthstone-turn-started",
    "hearthstone.card_played": "hearthstone-card-played",
    "hearthstone.player_damaged": "hearthstone-player-damaged",
    "hearthstone.player_healed": "hearthstone-player-healed",
    "hearthstone.match_won": "hearthstone-match-won",
    "hearthstone.match_lost": "hearthstone-match-lost",
}

DETAIL = re.compile(r"\s-\s(?P<detail>.+)$")
PLAYER_ENTITY = re.compile(r"Player\s+EntityID=(?P<entity>\d+)\s+PlayerID=(?P<player>\d+)")
TAG_CHANGE = re.compile(r"TAG_CHANGE\s+Entity=(?P<entity>.+?)\s+tag=(?P<tag>\w+)\s+value=(?P<value>\S+)")
BLOCK_PLAY = re.compile(r"BLOCK_START\s+BlockType=PLAY\s+Entity=(?P<entity>.+?)(?:\s+EffectCardId=|$)")
ENTITY_ID = re.compile(r"(?:id|entityID)=(?P<value>\d+)", re.IGNORECASE)
CARD_ID = re.compile(r"cardId=(?P<value>[^\s\]]*)", re.IGNORECASE)
PLAYER_ID = re.compile(r"player=(?P<value>\d+)", re.IGNORECASE)


def _registry_install_paths() -> list[Path]:
    if os.name != "nt":
        return []
    try:
        import winreg
    except ImportError:
        return []
    paths: list[Path] = []
    for hive in (winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE):
        for key_name in (
            r"SOFTWARE\Blizzard Entertainment\Hearthstone",
            r"SOFTWARE\WOW6432Node\Blizzard Entertainment\Hearthstone",
        ):
            try:
                with winreg.OpenKey(hive, key_name) as key:
                    value, _ = winreg.QueryValueEx(key, "InstallPath")
                    paths.append(Path(value))
            except OSError:
                continue
    return paths


def log_roots() -> list[Path]:
    configured = os.environ.get("HEARTHSTONE_POWER_LOG", "").strip()
    if configured:
        return [Path(configured).expanduser()]
    local = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData/Local"))
    return [
        local / "Blizzard/Hearthstone/Logs",
        *[path / "Logs" for path in _registry_install_paths()],
        Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Hearthstone/Logs",
    ]


def power_log_config_path() -> Path:
    configured = os.environ.get("HEARTHSTONE_LOG_CONFIG", "").strip()
    if configured:
        return Path(configured).expanduser()
    local = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData/Local"))
    return local / "Blizzard/Hearthstone/log.config"


def ensure_power_logging() -> bool:
    path = power_log_config_path()
    if path.exists():
        return False
    # 仅在配置完全不存在时创建，避免覆盖用户或其他工具的日志设置。
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        "[Power]\n"
        "LogLevel=1\n"
        "FilePrinting=true\n"
        "ConsolePrinting=false\n"
        "ScreenPrinting=false\n"
        "Verbose=true\n",
        encoding="utf-8",
    )
    return True


def find_log() -> Path | None:
    candidates: list[Path] = []
    for root in log_roots():
        if root.is_file():
            candidates.append(root)
        elif root.is_dir():
            candidates.extend(root.glob("Power.log"))
            candidates.extend(root.glob("*/Power.log"))
    return max(candidates, key=lambda path: path.stat().st_mtime) if candidates else None


def _entity_value(pattern: re.Pattern[str], entity: str) -> str:
    match = pattern.search(entity)
    return match.group("value") if match else ""


class PowerLogParser:
    def __init__(self) -> None:
        self.local_player_id = 1
        self.players_by_entity: dict[str, int] = {}
        self.damage_by_entity: dict[str, int] = {}
        self.last_turn = 0

    def reset(self) -> None:
        self.players_by_entity.clear()
        self.damage_by_entity.clear()
        self.last_turn = 0

    def feed(self, line: str) -> tuple[str, dict[str, Any]] | None:
        detail_match = DETAIL.search(line)
        detail = detail_match.group("detail").strip() if detail_match else line.strip()
        if detail == "CREATE_GAME":
            self.reset()
            return "hearthstone.match_started", {}

        player_match = PLAYER_ENTITY.search(detail)
        if player_match:
            self.players_by_entity[player_match.group("entity")] = int(player_match.group("player"))
            return None

        play_match = BLOCK_PLAY.search(detail)
        if play_match:
            entity = play_match.group("entity")
            if self._entity_player(entity) != self.local_player_id:
                return None
            return "hearthstone.card_played", {
                "entityId": _entity_value(ENTITY_ID, entity),
                "cardId": _entity_value(CARD_ID, entity),
            }

        tag_match = TAG_CHANGE.search(detail)
        if not tag_match:
            return None
        entity = tag_match.group("entity")
        tag = tag_match.group("tag")
        value = tag_match.group("value")

        if tag == "TURN" and value.isdigit():
            turn = int(value)
            if turn <= self.last_turn:
                return None
            self.last_turn = turn
            return "hearthstone.turn_started", {"turn": turn}

        if tag == "DAMAGE" and value.isdigit() and self._is_local_hero(entity):
            entity_id = _entity_value(ENTITY_ID, entity)
            current = int(value)
            previous = self.damage_by_entity.get(entity_id, 0)
            self.damage_by_entity[entity_id] = current
            if current > previous:
                return "hearthstone.player_damaged", {"amount": current - previous, "damage": current}
            if current < previous:
                return "hearthstone.player_healed", {"amount": previous - current, "damage": current}

        if tag == "PLAYSTATE" and value in {"WON", "LOST"}:
            if self._entity_player(entity) != self.local_player_id:
                return None
            key = "hearthstone.match_won" if value == "WON" else "hearthstone.match_lost"
            return key, {}
        return None

    def _entity_player(self, entity: str) -> int:
        direct = _entity_value(PLAYER_ID, entity)
        if direct:
            return int(direct)
        entity_id = _entity_value(ENTITY_ID, entity)
        if not entity_id and entity.strip().isdigit():
            entity_id = entity.strip()
        return self.players_by_entity.get(entity_id, 0)

    def _is_local_hero(self, entity: str) -> bool:
        card_id = _entity_value(CARD_ID, entity)
        return card_id.startswith("HERO_") and self._entity_player(entity) == self.local_player_id


def main() -> None:
    if ensure_power_logging():
        print("已创建炉石传说 Power.log 配置，请重新启动一次游戏。", flush=True)
    gateway = GatewayClient()
    parser = PowerLogParser()
    session_id = f"hearthstone-{uuid.uuid4()}"
    stream = None
    path: Path | None = None
    while True:
        gateway.maintain()
        latest = find_log()
        if stream is None and latest:
            path = latest
            stream = path.open("r", encoding="utf-8", errors="replace")
            # 只处理启动后的新日志，不重放上一局结果。
            stream.seek(0, os.SEEK_END)
        if stream is None:
            time.sleep(1)
            continue
        line = stream.readline()
        if line:
            parsed = parser.feed(line)
            if parsed:
                key, data = parsed
                gateway.send(build_event(SOURCE, key, COMMANDS[key], data, session_id))
        elif latest != path or (path and path.stat().st_size < stream.tell()):
            stream.close()
            stream = None
            parser.reset()
        time.sleep(0.1)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
