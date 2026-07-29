from __future__ import annotations

import os
import socket
import struct
import sys
import uuid
from pathlib import Path
from typing import Any

try:
    from yokonex_event_client import GatewayClient, build_event
except ModuleNotFoundError:
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "_shared"))
    from yokonex_event_client import GatewayClient, build_event


SOURCE = "f1"
UDP_PORT = int(os.environ.get("F1_UDP_PORT", "20777"))
HEADER = struct.Struct("<HBBBBBQfIIBB")
COMMANDS = {
    "f1.session_started": "f1-session-started",
    "f1.session_ended": "f1-session-ended",
    "f1.collision": "f1-collision",
    "f1.fastest_lap": "f1-fastest-lap",
    "f1.retirement": "f1-retirement",
    "f1.penalty": "f1-penalty",
    "f1.overtake": "f1-overtake",
    "f1.safety_car": "f1-safety-car",
    "f1.lights_out": "f1-lights-out",
    "f1.race_winner": "f1-race-winner",
    "f1.drs_enabled": "f1-drs-enabled",
    "f1.drs_disabled": "f1-drs-disabled",
}
SIMPLE_CODES = {
    b"SSTA": "f1.session_started",
    b"SEND": "f1.session_ended",
    b"DRSE": "f1.drs_enabled",
    b"DRSD": "f1.drs_disabled",
    b"LGOT": "f1.lights_out",
}


def parse_packet(packet: bytes) -> tuple[str, dict[str, Any]] | None:
    if len(packet) < HEADER.size + 4:
        return None
    values = HEADER.unpack_from(packet)
    packet_format, game_year, major, minor, packet_version, packet_id = values[:6]
    if packet_format not in (2024, 2025) or packet_id != 3:
        return None

    player_index = values[-2]
    code = packet[HEADER.size : HEADER.size + 4]
    details = packet[HEADER.size + 4 :]
    data: dict[str, Any] = {
        "packetFormat": packet_format,
        "gameYear": game_year,
        "gameVersion": f"{major}.{minor}",
        "packetVersion": packet_version,
        "playerCarIndex": player_index,
    }
    if code in SIMPLE_CODES:
        return SIMPLE_CODES[code], data
    if code == b"COLL" and len(details) >= 2:
        first, second = details[0], details[1]
        data.update(vehicle1Idx=first, vehicle2Idx=second, playerInvolved=player_index in (first, second))
        return "f1.collision", data
    if code == b"FTLP" and len(details) >= 5:
        vehicle, lap_time = struct.unpack_from("<Bf", details)
        data.update(vehicleIdx=vehicle, lapTime=round(lap_time, 3), isPlayer=vehicle == player_index)
        return "f1.fastest_lap", data
    if code == b"RTMT" and details:
        data.update(vehicleIdx=details[0], reason=details[1] if len(details) > 1 else 0)
        data["isPlayer"] = details[0] == player_index
        return "f1.retirement", data
    if code == b"PENA" and len(details) >= 7:
        penalty, infringement, vehicle, other, seconds, lap, places = struct.unpack_from("<BBBBBBB", details)
        data.update(
            penaltyType=penalty,
            infringementType=infringement,
            vehicleIdx=vehicle,
            otherVehicleIdx=other,
            time=seconds,
            lapNum=lap,
            placesGained=places,
            isPlayer=vehicle == player_index,
        )
        return "f1.penalty", data
    if code == b"OVTK" and len(details) >= 2:
        overtaking, overtaken = details[0], details[1]
        data.update(
            overtakingVehicleIdx=overtaking,
            beingOvertakenVehicleIdx=overtaken,
            playerInvolved=player_index in (overtaking, overtaken),
            playerCompleted=overtaking == player_index,
        )
        return "f1.overtake", data
    if code == b"SCAR" and len(details) >= 2:
        data.update(safetyCarType=details[0], eventType=details[1])
        return "f1.safety_car", data
    if code == b"RCWN" and details:
        data.update(vehicleIdx=details[0], isPlayer=details[0] == player_index)
        return "f1.race_winner", data
    return None


def main() -> None:
    client = GatewayClient()
    session_id = f"f1-{uuid.uuid4()}"
    listener = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    listener.bind(("0.0.0.0", UDP_PORT))
    listener.settimeout(0.5)
    while True:
        client.maintain()
        try:
            packet, _ = listener.recvfrom(65535)
        except TimeoutError:
            continue
        parsed = parse_packet(packet)
        if parsed:
            event_key, data = parsed
            client.send(build_event(SOURCE, event_key, COMMANDS[event_key], data, session_id))


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
