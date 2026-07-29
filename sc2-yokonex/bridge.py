from __future__ import annotations

import ctypes
import os
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


SOURCE = "sc2"
PROCESS_NAMES = {"sc2_x64.exe", "sc2.exe"}
COMMANDS = {
    "sc2.game_started": "sc2-game-started",
    "sc2.game_focused": "sc2-game-focused",
    "sc2.game_blurred": "sc2-game-blurred",
    "sc2.game_exited": "sc2-game-exited",
}

TH32CS_SNAPPROCESS = 0x00000002
INVALID_HANDLE_VALUE = ctypes.c_void_p(-1).value
MAX_PATH = 260


class PROCESSENTRY32W(ctypes.Structure):
    _fields_ = [
        ("dwSize", wintypes.DWORD),
        ("cntUsage", wintypes.DWORD),
        ("th32ProcessID", wintypes.DWORD),
        ("th32DefaultHeapID", ctypes.c_size_t),
        ("th32ModuleID", wintypes.DWORD),
        ("cntThreads", wintypes.DWORD),
        ("th32ParentProcessID", wintypes.DWORD),
        ("pcPriClassBase", wintypes.LONG),
        ("dwFlags", wintypes.DWORD),
        ("szExeFile", wintypes.WCHAR * MAX_PATH),
    ]


def starcraft_process_ids() -> set[int]:
    """读取 SC2 进程，不依赖 psutil、命令行输出或录像文件。"""
    if os.name != "nt":
        return set()
    kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel32.CreateToolhelp32Snapshot.argtypes = [wintypes.DWORD, wintypes.DWORD]
    kernel32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
    kernel32.Process32FirstW.argtypes = [wintypes.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
    kernel32.Process32FirstW.restype = wintypes.BOOL
    kernel32.Process32NextW.argtypes = [wintypes.HANDLE, ctypes.POINTER(PROCESSENTRY32W)]
    kernel32.Process32NextW.restype = wintypes.BOOL
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL

    snapshot = kernel32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    if snapshot == INVALID_HANDLE_VALUE:
        return set()
    entry = PROCESSENTRY32W()
    entry.dwSize = ctypes.sizeof(entry)
    result: set[int] = set()
    try:
        found = kernel32.Process32FirstW(snapshot, ctypes.byref(entry))
        while found:
            if entry.szExeFile.casefold() in PROCESS_NAMES:
                result.add(int(entry.th32ProcessID))
            found = kernel32.Process32NextW(snapshot, ctypes.byref(entry))
    finally:
        kernel32.CloseHandle(snapshot)
    return result


def foreground_process_id() -> int | None:
    if os.name != "nt":
        return None
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    user32.GetForegroundWindow.restype = wintypes.HWND
    user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user32.GetWindowThreadProcessId.restype = wintypes.DWORD
    window = user32.GetForegroundWindow()
    if not window:
        return None
    process_id = wintypes.DWORD()
    user32.GetWindowThreadProcessId(window, ctypes.byref(process_id))
    return int(process_id.value) or None


class GameStateTracker:
    """只在状态边沿生成一次事件，避免轮询期间重复触发指令。"""

    def __init__(self) -> None:
        self.running = False
        self.focused = False

    def update(
        self,
        process_ids: set[int],
        foreground_id: int | None,
    ) -> list[tuple[str, dict[str, Any]]]:
        running = bool(process_ids)
        focused = running and foreground_id in process_ids
        events: list[tuple[str, dict[str, Any]]] = []

        if running and not self.running:
            events.append(("sc2.game_started", {"processCount": len(process_ids)}))
        if running and focused != self.focused:
            key = "sc2.game_focused" if focused else "sc2.game_blurred"
            events.append((key, {}))
        if not running and self.running:
            events.append(("sc2.game_exited", {}))

        self.running = running
        self.focused = focused
        return events


def main() -> None:
    if os.name != "nt":
        raise SystemExit("星际争霸 II 插件仅支持 Windows")
    gateway = GatewayClient()
    tracker = GameStateTracker()
    session_id = f"sc2-{uuid.uuid4()}"
    while True:
        gateway.maintain()
        process_ids = starcraft_process_ids()
        # 前台判断使用 PID，避免依赖本地化窗口标题。
        for event_key, data in tracker.update(process_ids, foreground_process_id()):
            gateway.send(build_event(SOURCE, event_key, COMMANDS[event_key], data, session_id))
        time.sleep(0.5)


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
