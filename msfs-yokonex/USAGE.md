# 微软模拟飞行 2020 / 2024 接入说明

适用于 Windows 版 Microsoft Flight Simulator 2020 和 2024，支持固定翼、直升机及第三方飞机。事件是否可用取决于飞机是否正确提供标准 SimVar。

1. 在模拟器中打开开发者模式，从开发者菜单安装官方 SDK。
2. 在 GameHub 插件市场安装本插件。
3. 双击 `一键安装.bat`。SDK 不在默认目录时，运行 `install.ps1 -SdkPath "SDK目录"`。
4. 启动 GameHub，再启动模拟器并进入驾驶舱。

不需要把文件放入 Community 目录。本插件不分发微软的 SDK 文件。

如果一直没有连接：

- 确认模拟器和 GameHub 运行在同一台电脑。
- 不要让其中一个程序以管理员身份运行、另一个不以管理员身份运行。
- 可用环境变量 `SIMCONNECT_DLL` 指定官方 `SimConnect.dll` 的完整路径。
