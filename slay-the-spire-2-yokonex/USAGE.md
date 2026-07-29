# 杀戮尖塔2接入说明

适用于 Windows Steam 版。市场包自带 `Yokonex-STS2-Events` 独立事件 Mod，
同时保留官方 `godot.log` 和当前存档作为兜底。

1. 在 GameHub 市场安装并启用本插件。
2. 双击插件目录中的 `一键安装.bat`，安装独立事件 Mod。
3. 先启动 GameHub，再以“启用 Mods”模式启动《杀戮尖塔2》。

默认读取 `%APPDATA%\SlayTheSpire2`。特殊安装可设置环境变量 `STS2_DATA_ROOT`。

Mod 运行时，受伤、治疗、死亡、能量、格挡、球体、奖励、商店和事件均来自游戏内挂钩。
Mod 未运行时，仍可获取日志和存档支持的基础事件，但覆盖范围较少。

事件 Mod 只采集事件，不提供游戏控制、设置页、IM 或蓝牙功能。
开发者可运行 `build-mod.ps1`，使用本机游戏程序集重新生成 DLL。
