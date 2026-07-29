# 星露谷物语联动使用教程

## 前置条件

- Windows 10 或 Windows 11 64 位系统。
- 星露谷物语 1.6 或更高版本。
- SMAPI 4.0 或更高版本，并且游戏目录中已有 `Mods` 文件夹。
- Yokonex-Gamehub 已安装，状态页显示“网关运行中”。
- 如需发送 IM 指令，先在“配置”页面填写并保存 IM 配置。
- 如需蓝牙反馈，先在“蓝牙”页面连接设备。
- 用户电脑不需要安装 .NET SDK，Mod 已随网关预编译。

## 第一步：配置联动事件

1. 启动 Yokonex-Gamehub。
2. 打开“联动”页面，找到“星露谷物语”。
3. 点击“配置”。
4. 检查每个事件的 `commandId`。
5. 按需启用或禁用单个事件，并为需要蓝牙反馈的事件选择波形。
6. 保存配置。
7. 在卡片外层打开“启用游戏联动”开关。

## 第二步：安装 Mod

1. 先退出星露谷物语和 SMAPI。
2. 在星露谷物语卡片点击“安装 SMAPI Mod”。
3. 选择包含 `Stardew Valley.dll` 和 `Mods` 文件夹的游戏目录。
4. 等待“SMAPI Mod 已安装”提示。
5. 安装完成后，目录中应存在：

```text
Stardew Valley\Mods\StardewYokonex\
├─ StardewYokonex.dll
├─ manifest.json
└─ USAGE.md
```

## 第三步：启动游戏

1. 保持 Yokonex-Gamehub 运行。
2. 通过 `StardewModdingAPI.exe` 启动游戏；如果 Steam 已配置 SMAPI 启动项，也可以直接从 Steam 启动。
3. 在 SMAPI 控制台确认出现“Yokonex 星露谷联动已加载”。
4. 载入存档并触发一天开始、获得物品或切换地点等事件。

## 第四步：确认联动

1. 返回网关“联动”页面。
2. 检查星露谷卡片的事件数量和最后接收时间是否更新。
3. 如果事件已收到但蓝牙没有动作，检查该事件是否启用、是否选择波形，以及蓝牙设备是否在线。
4. 如果 IM 没有响应，检查对应 `commandId` 和“配置”页面中的 IM 设置。

## 常见问题

- 提示目录不正确：必须选择游戏根目录，不能选择 `Mods` 或某个存档目录。
- 找不到 `Mods`：先安装并运行一次 SMAPI。
- SMAPI 没有加载 Mod：确认 `StardewYokonex.dll` 和 `manifest.json` 位于同一层目录。
- 网关没有事件：先启动网关，再通过 SMAPI 启动游戏，并确认外层联动开关已开启。
- 更新网关后仍使用旧 Mod：再次点击“安装 SMAPI Mod”覆盖安装。
