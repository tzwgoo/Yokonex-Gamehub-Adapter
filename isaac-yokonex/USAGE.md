# 以撒的结合：忏悔联动

## 功能

Lua Mod 采集开局、结束、楼层、房间、受伤、治疗、死亡、复活、角色切换、道具变化和 Boss 事件。桥接器读取本机 `log.txt`，通过 WebSocket 发送到 GameHub。

支持 Repentance/Repentance+ 全部官方角色和本地合作模式：

- 雅各与以扫、遗骸与灵魂等角色会分别标记 `playerIndex` 和 `playerRole`。
- 拉撒路、里拉撒路等形态变化会触发 `isaac.character_changed`。
- 每个玩家事件都包含 `playerType`、`characterName` 和 `controllerIndex`。
- 红心、魂心、骨心和特殊充能会分别写入 `vitals`。
- 背包快照支持底座拾取、D4/D100 重投及其他 Mod 给予或移除道具。

自定义角色也会按 Mod 提供的角色名称和类型上报，但自定义生命机制无法保证与官方角色相同。

## 环境要求

- Windows 10/11
- 《The Binding of Isaac: Repentance》或 Repentance+
- Yokonex GameHub
- Python 3.10 或更高版本

不需要 REPENTOGON，不读取游戏内存，不连接公网。

## 安装

1. 在 GameHub 插件市场安装本插件。
2. 打开插件目录，双击 `一键安装.bat`。
3. 如果脚本没有找到 Steam 游戏目录，用 PowerShell 执行：

   ```powershell
   .\install.ps1 -GameDir "D:\SteamLibrary\steamapps\common\The Binding of Isaac Rebirth"
   ```

4. 启动游戏，在 `Mods` 菜单启用 `Yokonex GameHub Link`。
5. 在 GameHub 的联动页启用“以撒的结合：忏悔”，配置事件指令并保存。

## WebSocket

桥接器默认连接：

```text
ws://127.0.0.1:43002/v1/events
```

GameHub 会自动启动 `bridge.py`，并传入实际网关地址。断线后按 `1、2、4、8、15` 秒退避重连，离线期间不会补发过期事件。

## 验证

1. 保持 GameHub 开启并进入一局游戏。
2. 进入新房间或受到一次伤害。
3. GameHub 日志应出现 `source=isaac` 和对应的 `eventKey`。

## 常见问题

- 没有事件：确认游戏 Mods 菜单中已启用本 Mod，并重启一局。
- 找不到日志：设置环境变量 `ISAAC_LOG_PATH` 为游戏的 `log.txt` 完整路径。
- WebSocket 连接失败：确认 GameHub 网关已启动，端口没有被防火墙拦截。
- 显示 `event_disabled`：在 GameHub 联动页启用对应事件并保存。
- 桥接器无法启动：安装 Python 3.10+，或配置 `YOKONEX_PLUGIN_PYTHON`。

## 卸载

1. 在 GameHub 中卸载插件。
2. 删除游戏目录下的 `mods\yokonex-isaac-events`。
