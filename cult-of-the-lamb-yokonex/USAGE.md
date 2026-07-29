# 咩咩启示录接入说明

支持 Windows Steam 版和本地双人模式，依赖 BepInExPack CultOfTheLamb 5.4.21。

1. 通过 Thunderstore 或 r2modman 安装 `BepInExPack CultOfTheLamb`。
2. 至少用 Mod 管理器启动一次游戏，确认 BepInEx 正常加载。
3. 在 GameHub 市场安装本插件，双击 `一键安装.bat`。
4. 先启动 GameHub，再通过 Mod 管理器启动游戏。

安装器会使用游戏和 BepInEx 自带的程序集在本机编译事件 Mod，不会分发游戏 DLL。

事件日志位于游戏目录 `BepInEx\yokonex_events.log`。游戏更新后如果事件失效，重新运行安装脚本。
