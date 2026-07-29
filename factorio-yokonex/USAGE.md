# Factorio 接入说明

支持 Factorio 2.x，单人、联机和所有玩家角色共用同一套事件。

1. 在 GameHub 插件市场安装本插件。
2. 双击 `一键安装.bat`。
3. 在 Steam 的 Factorio 启动选项加入 `--enable-lua-udp`。
4. 启动 GameHub，再启动 Factorio。

桥接器只监听 `127.0.0.1:34198`。可用环境变量 `YOKONEX_GATEWAY_URL` 修改 GameHub 地址。

如果没有事件，检查 Factorio 日志是否出现 `[YOKONEX_FACTORIO]`。出现 UDP 权限错误时，说明启动参数没有生效。
