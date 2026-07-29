from __future__ import annotations

import json
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


SOURCE = "slay_the_spire"
COMMANDS = {
    key: key.replace(".", "-").replace("_", "-")
    for key in (
        "slay_the_spire.run_started",
        "slay_the_spire.run_ended",
        "slay_the_spire.floor_entered",
        "slay_the_spire.combat_started",
        "slay_the_spire.combat_won",
        "slay_the_spire.player_damaged",
        "slay_the_spire.player_healed",
        "slay_the_spire.turn_started",
        "slay_the_spire.monster_killed",
        "slay_the_spire.card_gained",
        "slay_the_spire.card_removed",
        "slay_the_spire.relic_gained",
        "slay_the_spire.potion_gained",
        "slay_the_spire.gold_changed",
    )
}


def _items(values: list[dict[str, Any]], empty_id: str = "") -> Counter[str]:
    return Counter(
        f"{item.get('id', '')}|{item.get('upgrades', 0)}"
        for item in values
        if item.get("id") and item.get("id") != empty_id
    )


def _new_items(before: Counter[str], after: Counter[str]) -> list[str]:
    return list((after - before).elements())


def changed_events(
    previous: dict[str, Any] | None, message: dict[str, Any]
) -> list[tuple[str, dict[str, Any]]]:
    in_game = bool(message.get("in_game"))
    current = message.get("game_state") if in_game else None
    if not isinstance(current, dict):
        if previous:
            return [("slay_the_spire.run_ended", {"floor": previous.get("floor", 0)})]
        return []
    if previous is None:
        return [
            (
                "slay_the_spire.run_started",
                {
                    "character": current.get("class"),
                    "ascension": current.get("ascension_level"),
                    "seed": current.get("seed"),
                },
            )
        ]

    events: list[tuple[str, dict[str, Any]]] = []
    if current.get("floor") != previous.get("floor"):
        events.append(
            (
                "slay_the_spire.floor_entered",
                {"floor": current.get("floor"), "act": current.get("act"), "room": current.get("room_type")},
            )
        )
    old_phase, new_phase = previous.get("room_phase"), current.get("room_phase")
    if old_phase != "COMBAT" and new_phase == "COMBAT":
        events.append(("slay_the_spire.combat_started", {"floor": current.get("floor")}))
    if old_phase == "COMBAT" and new_phase != "COMBAT" and current.get("current_hp", 0) > 0:
        events.append(("slay_the_spire.combat_won", {"floor": current.get("floor")}))

    old_hp, new_hp = previous.get("current_hp", 0), current.get("current_hp", 0)
    if new_hp < old_hp:
        events.append(("slay_the_spire.player_damaged", {"amount": old_hp - new_hp, "health": new_hp}))
    elif new_hp > old_hp:
        events.append(("slay_the_spire.player_healed", {"amount": new_hp - old_hp, "health": new_hp}))

    old_combat = previous.get("combat_state") or {}
    new_combat = current.get("combat_state") or {}
    if new_combat.get("turn") and new_combat.get("turn") != old_combat.get("turn"):
        events.append(("slay_the_spire.turn_started", {"turn": new_combat.get("turn")}))
    old_monsters = old_combat.get("monsters") or []
    for index, monster in enumerate(new_combat.get("monsters") or []):
        old_monster = old_monsters[index] if index < len(old_monsters) else {}
        if old_monster.get("current_hp", 0) > 0 and (
            monster.get("current_hp", 0) <= 0 or monster.get("is_gone")
        ):
            events.append(("slay_the_spire.monster_killed", {"id": monster.get("id"), "name": monster.get("name")}))

    old_deck, new_deck = _items(previous.get("deck") or []), _items(current.get("deck") or [])
    for card in _new_items(old_deck, new_deck):
        card_id, upgrades = card.rsplit("|", 1)
        events.append(("slay_the_spire.card_gained", {"id": card_id, "upgrades": int(upgrades)}))
    for card in _new_items(new_deck, old_deck):
        card_id, upgrades = card.rsplit("|", 1)
        events.append(("slay_the_spire.card_removed", {"id": card_id, "upgrades": int(upgrades)}))

    old_relics, new_relics = _items(previous.get("relics") or []), _items(current.get("relics") or [])
    for relic in _new_items(old_relics, new_relics):
        events.append(("slay_the_spire.relic_gained", {"id": relic.split("|", 1)[0]}))
    old_potions = _items(previous.get("potions") or [], "Potion Slot")
    new_potions = _items(current.get("potions") or [], "Potion Slot")
    for potion in _new_items(old_potions, new_potions):
        events.append(("slay_the_spire.potion_gained", {"id": potion.split("|", 1)[0]}))
    if current.get("gold") != previous.get("gold"):
        events.append(
            (
                "slay_the_spire.gold_changed",
                {"amount": current.get("gold", 0) - previous.get("gold", 0), "gold": current.get("gold", 0)},
            )
        )
    return events


def stdio_main() -> None:
    gateway = GatewayClient()
    session_id = f"slay-the-spire-{uuid.uuid4()}"
    previous: dict[str, Any] | None = None
    print("ready", flush=True)
    for line in sys.stdin:
        try:
            message = json.loads(line)
        except json.JSONDecodeError:
            print("wait 30", flush=True)
            continue
        for event_key, data in changed_events(previous, message):
            gateway.send(build_event(SOURCE, event_key, COMMANDS[event_key], data, session_id))
        state = message.get("game_state") if message.get("in_game") else None
        previous = state if isinstance(state, dict) else None
        # CommunicationMod 每收到一条状态后必须收到下一条命令。
        print("wait 30", flush=True)


def main() -> None:
    if "--stdio" in sys.argv:
        stdio_main()
        return
    # GameHub 保持插件进程存活；真正的状态进程由 CommunicationMod 启动。
    while True:
        time.sleep(30)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
