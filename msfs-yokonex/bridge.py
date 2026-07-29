from __future__ import annotations

import ctypes
import os
import struct
import sys
import time
import uuid
from ctypes import wintypes
from pathlib import Path
from typing import Any

try:
    from yokonex_event_client import GatewayClient, build_event
except ModuleNotFoundError:
    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "_shared"))
    from yokonex_event_client import GatewayClient, build_event


SOURCE = "msfs"
REQUEST_AIRCRAFT = 1
DEFINITION_AIRCRAFT = 1
SIMCONNECT_RECV_ID_QUIT = 3
SIMCONNECT_RECV_ID_SIMOBJECT_DATA = 8
SIMCONNECT_OBJECT_ID_USER = 0
SIMCONNECT_PERIOD_SECOND = 4
SIMCONNECT_DATATYPE_FLOAT64 = 4
DATA_NAMES = (
    ("SIM ON GROUND", "Bool"),
    ("PLANE ALTITUDE", "feet"),
    ("AIRSPEED INDICATED", "knots"),
    ("VERTICAL SPEED", "feet per minute"),
    ("STALL WARNING", "Bool"),
    ("OVERSPEED WARNING", "Bool"),
    ("CRASH FLAG", "Number"),
    ("GENERAL ENG COMBUSTION:1", "Bool"),
    ("FUEL TOTAL QUANTITY PERCENT", "Percent"),
)
COMMANDS = {
    "msfs.connected": "msfs-connected",
    "msfs.takeoff": "msfs-takeoff",
    "msfs.landed": "msfs-landed",
    "msfs.stall_warning": "msfs-stall-warning",
    "msfs.overspeed_warning": "msfs-overspeed-warning",
    "msfs.crashed": "msfs-crashed",
    "msfs.engine_started": "msfs-engine-started",
    "msfs.engine_stopped": "msfs-engine-stopped",
    "msfs.low_fuel": "msfs-low-fuel",
}


def changed_events(
    previous: dict[str, Any] | None, current: dict[str, Any]
) -> list[tuple[str, dict[str, Any]]]:
    if previous is None:
        return [("msfs.connected", {"altitudeFeet": current["altitudeFeet"]})]
    events: list[tuple[str, dict[str, Any]]] = []
    flight = {
        "altitudeFeet": current["altitudeFeet"],
        "airspeedKnots": current["airspeedKnots"],
        "verticalSpeedFpm": current["verticalSpeedFpm"],
    }
    if previous["onGround"] and not current["onGround"] and current["airspeedKnots"] > 30:
        events.append(("msfs.takeoff", flight))
    if not previous["onGround"] and current["onGround"]:
        events.append(("msfs.landed", {**flight, "touchdownVerticalSpeedFpm": current["verticalSpeedFpm"]}))
    for field, event_key in (
        ("stallWarning", "msfs.stall_warning"),
        ("overspeedWarning", "msfs.overspeed_warning"),
    ):
        if not previous[field] and current[field]:
            events.append((event_key, flight))
    if previous["crashFlag"] == 0 and current["crashFlag"] != 0:
        events.append(("msfs.crashed", {**flight, "crashFlag": current["crashFlag"]}))
    if previous["engineRunning"] != current["engineRunning"]:
        key = "msfs.engine_started" if current["engineRunning"] else "msfs.engine_stopped"
        events.append((key, flight))
    if previous["fuelPercent"] > 10 >= current["fuelPercent"]:
        events.append(("msfs.low_fuel", {"fuelPercent": current["fuelPercent"]}))
    return events


def parse_aircraft_data(raw: bytes) -> dict[str, Any]:
    if len(raw) < 8 * len(DATA_NAMES):
        raise ValueError("SimConnect 飞行数据长度不足")
    values = struct.unpack_from("<9d", raw)
    return {
        "onGround": values[0] > 0.5,
        "altitudeFeet": round(values[1], 1),
        "airspeedKnots": round(values[2], 1),
        "verticalSpeedFpm": round(values[3], 1),
        "stallWarning": values[4] > 0.5,
        "overspeedWarning": values[5] > 0.5,
        "crashFlag": int(values[6]),
        "engineRunning": values[7] > 0.5,
        "fuelPercent": round(values[8], 1),
    }


class SimConnectClient:
    def __init__(self) -> None:
        dll_path = find_simconnect_dll()
        self.api = ctypes.WinDLL(dll_path)
        self.handle = wintypes.HANDLE()
        self._configure_api()

    def _configure_api(self) -> None:
        self.api.SimConnect_Open.argtypes = [
            ctypes.POINTER(wintypes.HANDLE),
            wintypes.LPCSTR,
            wintypes.HWND,
            wintypes.DWORD,
            wintypes.HANDLE,
            wintypes.DWORD,
        ]
        self.api.SimConnect_Open.restype = wintypes.HRESULT
        self.api.SimConnect_Close.argtypes = [wintypes.HANDLE]
        self.api.SimConnect_Close.restype = wintypes.HRESULT
        self.api.SimConnect_AddToDataDefinition.argtypes = [
            wintypes.HANDLE,
            wintypes.DWORD,
            wintypes.LPCSTR,
            wintypes.LPCSTR,
            wintypes.DWORD,
            ctypes.c_float,
            wintypes.DWORD,
        ]
        self.api.SimConnect_AddToDataDefinition.restype = wintypes.HRESULT
        self.api.SimConnect_RequestDataOnSimObject.argtypes = [
            wintypes.HANDLE,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.DWORD,
        ]
        self.api.SimConnect_RequestDataOnSimObject.restype = wintypes.HRESULT
        self.api.SimConnect_GetNextDispatch.argtypes = [
            wintypes.HANDLE,
            ctypes.POINTER(ctypes.c_void_p),
            ctypes.POINTER(wintypes.DWORD),
        ]
        self.api.SimConnect_GetNextDispatch.restype = wintypes.HRESULT

    def connect(self) -> None:
        result = self.api.SimConnect_Open(
            ctypes.byref(self.handle), b"Yokonex GameHub", None, 0, None, 0
        )
        if result < 0:
            raise ConnectionError(f"SimConnect_Open 失败：0x{result & 0xFFFFFFFF:08X}")
        for index, (name, unit) in enumerate(DATA_NAMES):
            result = self.api.SimConnect_AddToDataDefinition(
                self.handle,
                DEFINITION_AIRCRAFT,
                name.encode("ascii"),
                unit.encode("ascii"),
                SIMCONNECT_DATATYPE_FLOAT64,
                0.0,
                index,
            )
            if result < 0:
                self.close()
                raise ConnectionError(f"添加 SimVar 失败：{name}")
        result = self.api.SimConnect_RequestDataOnSimObject(
            self.handle,
            REQUEST_AIRCRAFT,
            DEFINITION_AIRCRAFT,
            SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD_SECOND,
            0,
            0,
            0,
            0,
        )
        if result < 0:
            self.close()
            raise ConnectionError("请求 SimConnect 飞行数据失败")

    def poll(self) -> tuple[str, dict[str, Any] | None] | None:
        pointer = ctypes.c_void_p()
        size = wintypes.DWORD()
        result = self.api.SimConnect_GetNextDispatch(
            self.handle, ctypes.byref(pointer), ctypes.byref(size)
        )
        if result < 0 or not pointer.value or size.value < 12:
            return None
        receive_id = ctypes.c_uint32.from_address(pointer.value + 8).value
        if receive_id == SIMCONNECT_RECV_ID_QUIT:
            return "quit", None
        if receive_id != SIMCONNECT_RECV_ID_SIMOBJECT_DATA or size.value < 40 + 72:
            return None
        request_id = ctypes.c_uint32.from_address(pointer.value + 12).value
        if request_id != REQUEST_AIRCRAFT:
            return None
        return "data", parse_aircraft_data(ctypes.string_at(pointer.value + 40, 72))

    def close(self) -> None:
        if self.handle.value:
            self.api.SimConnect_Close(self.handle)
            self.handle = wintypes.HANDLE()


def find_simconnect_dll() -> str:
    configured = os.environ.get("SIMCONNECT_DLL", "").strip()
    candidates = [
        Path(configured) if configured else None,
        Path(__file__).resolve().with_name("SimConnect.dll"),
        Path("C:/MSFS SDK/SimConnect SDK/lib/SimConnect.dll"),
        Path("C:/MSFS 2024 SDK/SimConnect SDK/lib/SimConnect.dll"),
    ]
    for candidate in candidates:
        if candidate and candidate.is_file():
            return str(candidate)
    # 兼容已由系统或其他飞行软件注册到 DLL 搜索路径的环境。
    return "SimConnect.dll"


def main() -> None:
    gateway = GatewayClient()
    session_id = f"msfs-{uuid.uuid4()}"
    simulator: SimConnectClient | None = None
    previous: dict[str, Any] | None = None
    while True:
        gateway.maintain()
        if simulator is None:
            try:
                simulator = SimConnectClient()
                simulator.connect()
            except (OSError, ConnectionError):
                simulator = None
                time.sleep(2)
                continue
        message = simulator.poll()
        if message:
            kind, current = message
            if kind == "quit":
                simulator.close()
                simulator = None
                previous = None
            elif current:
                for event_key, data in changed_events(previous, current):
                    gateway.send(build_event(SOURCE, event_key, COMMANDS[event_key], data, session_id))
                previous = current
        time.sleep(0.05)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
