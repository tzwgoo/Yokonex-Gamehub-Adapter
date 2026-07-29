from __future__ import annotations

import mmap
import struct
import time
import uuid
from typing import Any

from yokonex_event_client import GatewayClient, build_event


MMF_NAME = "Local\\SCSTelemetry"
MMF_SIZE = 32 * 1024
GAME_IDS = {"ets2": 1, "ats": 2}
COMMAND_SUFFIXES = {
    "connected": "connected",
    "job_started": "job-started",
    "job_delivered": "job-delivered",
    "job_cancelled": "job-cancelled",
    "fined": "fined",
    "toll_paid": "toll-paid",
    "ferry_used": "ferry-used",
    "train_used": "train-used",
    "refueled": "refueled",
    "fuel_warning": "fuel-warning",
    "engine_started": "engine-started",
    "engine_stopped": "engine-stopped",
}


def _u32(data: bytes, offset: int) -> int:
    return struct.unpack_from("<I", data, offset)[0]


def _i64(data: bytes, offset: int) -> int:
    return struct.unpack_from("<q", data, offset)[0]


def _f32(data: bytes, offset: int) -> float:
    return struct.unpack_from("<f", data, offset)[0]


def _text(data: bytes, offset: int, length: int) -> str:
    return data[offset : offset + length].split(b"\0", 1)[0].decode("utf-8", errors="replace")


def parse_snapshot(data: bytes) -> dict[str, Any]:
    if len(data) < 4400:
        raise ValueError("SCS 共享内存长度不足")
    flags = data[4300:4310]
    return {
        "active": bool(data[0]),
        "paused": bool(data[4]),
        "timestamp": struct.unpack_from("<Q", data, 8)[0],
        "pluginRevision": _u32(data, 40),
        "game": _u32(data, 52),
        "speedKph": round(abs(_f32(data, 948)) * 3.6, 2),
        "fuel": round(_f32(data, 1000), 2),
        "cargoDamage": round(_f32(data, 1468) * 100, 2),
        "cargo": _text(data, 2620, 64),
        "sourceCity": _text(data, 3004, 64),
        "destinationCity": _text(data, 2748, 64),
        "fineOffence": _text(data, 3436, 32),
        "jobRevenue": _i64(data, 4208),
        "fineAmount": _i64(data, 4216),
        "onJob": bool(flags[0]),
        "jobFinished": bool(flags[1]),
        "jobCancelled": bool(flags[2]),
        "jobDelivered": bool(flags[3]),
        "fined": bool(flags[4]),
        "tollgate": bool(flags[5]),
        "ferry": bool(flags[6]),
        "train": bool(flags[7]),
        "refuel": bool(flags[8]),
        "fuelWarning": bool(data[1570]),
        "engineEnabled": bool(data[1576]),
    }


def changed_events(
    previous: dict[str, Any] | None, current: dict[str, Any], source: str
) -> list[tuple[str, dict[str, Any]]]:
    if previous is None:
        return [("connected", {"pluginRevision": current["pluginRevision"]})]

    events: list[tuple[str, dict[str, Any]]] = []

    def toggled(name: str, event_name: str, fields: tuple[str, ...]) -> None:
        # 游戏玩法事件位每次发生都会翻转，不能只判断 False -> True。
        if current[name] != previous[name]:
            events.append((event_name, {field: current[field] for field in fields}))

    if not previous["onJob"] and current["onJob"]:
        events.append(
            (
                "job_started",
                {
                    "cargo": current["cargo"],
                    "sourceCity": current["sourceCity"],
                    "destinationCity": current["destinationCity"],
                },
            )
        )
    toggled("jobDelivered", "job_delivered", ("cargo", "cargoDamage", "jobRevenue"))
    toggled("jobCancelled", "job_cancelled", ("cargo",))
    toggled("fined", "fined", ("fineOffence", "fineAmount"))
    toggled("tollgate", "toll_paid", ())
    toggled("ferry", "ferry_used", ())
    toggled("train", "train_used", ())
    toggled("refuel", "refueled", ("fuel",))
    if not previous["fuelWarning"] and current["fuelWarning"]:
        events.append(("fuel_warning", {"fuel": current["fuel"]}))
    if current["engineEnabled"] != previous["engineEnabled"]:
        name = "engine_started" if current["engineEnabled"] else "engine_stopped"
        events.append((name, {"speedKph": current["speedKph"]}))
    return events


def run(source: str) -> None:
    if source not in GAME_IDS:
        raise ValueError(f"不支持的 SCS 来源: {source}")
    client = GatewayClient()
    session_id = f"{source}-{uuid.uuid4()}"
    previous: dict[str, Any] | None = None
    shared: mmap.mmap | None = None

    while True:
        client.maintain()
        if shared is None:
            try:
                shared = mmap.mmap(-1, MMF_SIZE, tagname=MMF_NAME, access=mmap.ACCESS_READ)
            except FileNotFoundError:
                time.sleep(1)
                continue
        try:
            shared.seek(0)
            current = parse_snapshot(shared.read(MMF_SIZE))
        except (OSError, ValueError):
            shared.close()
            shared = None
            previous = None
            continue
        if not current["active"] or current["game"] != GAME_IDS[source]:
            previous = None
            time.sleep(0.25)
            continue
        if previous and current["timestamp"] == previous["timestamp"]:
            time.sleep(0.1)
            continue
        for name, payload in changed_events(previous, current, source):
            event_key = f"{source}.{name}"
            command_id = f"{source}-{COMMAND_SUFFIXES[name]}"
            client.send(build_event(source, event_key, command_id, payload, session_id))
        previous = current
        time.sleep(0.1)
