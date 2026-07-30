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
4. 双击 `一键安装.bat`。
5. 输入包含 `SecretFlasherManaka.exe` 的游戏目录并回车。
6. 看到“安装完成”后关闭窗口。

安装完成后应存在以下文件：

```text
游戏目录\BepInEx\plugins\ManakaLinkYokonex\
├─ ManakaLinkYokonex.dll
├─ ManakaLinkYokonex.deps.json
├─ Microsoft.Windows.SDK.NET.dll
└─ WinRT.Runtime.dll
```

## 使用

1. 启动游戏。
2. 按 `F8` 打开 Mod 面板。
3. 在“连接”页填写 IM 的 UID、Token，并登录。
4. 在“事件”页启用事件并检查 `commandId`。
5. 如需直连蓝牙设备，在蓝牙相关页面完成扫描、连接和波形配置。

Mod 的事件开关、IM 登录和蓝牙规则以游戏内 `F8` 面板为准。

## 常见问题

- 提示游戏目录不正确：请选择直接包含 `SecretFlasherManaka.exe` 的目录。
- 提示未找到 BepInEx：先为游戏安装并运行一次 BepInEx 6 IL2CPP。
- 按 `F8` 没有面板：检查 `BepInEx\LogOutput.log` 中是否出现 `SecretFlasherManaka-Link-YOKONEX initialized`。
- 更新插件后仍运行旧版本：再次执行 `一键安装.bat` 覆盖游戏目录中的 Mod。
