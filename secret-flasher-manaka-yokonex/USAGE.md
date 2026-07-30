# Secret Flasher Manaka 联动使用教程

## 前置条件

- Windows 10 或 Windows 11 64 位系统。
- 《Secret Flasher Manaka》v1.0.6。
- 游戏目录已安装 BepInEx 6 IL2CPP。
- 已在 GameHub 插件市场安装“Secret Flasher Manaka”。

## 安装 Mod

1. 退出游戏。
2. 在 GameHub“插件中心 → 游戏联动”中找到“Secret Flasher Manaka”。
3. 点击“打开 Mod”。
4. 双击 `ManakaLinkInstaller.exe`。
5. 选择包含 `SecretFlasherManaka.exe` 的游戏目录。
6. 点击“安装 Mod”。

安装器会尝试识别正在运行过的游戏目录。安装完成后应存在：

```text
游戏目录\BepInEx\plugins\ManakaLinkYokonex\
├─ ManakaLinkYokonex.dll
└─ ManakaLinkYokonex.deps.json
```

## 使用

1. 在 GameHub 中启用该插件并配置事件映射。
2. 先启动 GameHub，再启动游戏。
3. Mod 会连接 `ws://127.0.0.1:43002/v1/events`，并按 GameHub 配置上报事件。
4. 游戏内按 `F8` 可查看连接状态、当前映射和最近事件。

所有事件开关、`commandId` 和设备联动都以 GameHub 配置为准。

## 常见问题

- 提示游戏目录不正确：请选择直接包含 `SecretFlasherManaka.exe` 的目录。
- 提示未找到 BepInEx：先为游戏安装并运行一次 BepInEx 6 IL2CPP。
- 显示 GameHub 未连接：确认 GameHub 已启动，并检查本机 43002 端口。
- 更新后仍运行旧版本：关闭游戏，再次运行 `ManakaLinkInstaller.exe` 覆盖安装。
