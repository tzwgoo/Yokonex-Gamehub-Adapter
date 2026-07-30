# Minecraft 联动安装说明

支持 Minecraft 1.20.1、Forge 47.3.0、Java 17。

下载后的分发包已经包含两个可直接使用的 Mod：

- `prebuilt/yokonex-gamehub-minecraft-1.1.1.jar`：仅单人游戏使用。
- `prebuilt/yokonex-gamehub-minecraft-server-1.1.1.jar`：局域网联机、专用服务器使用。

两个 JAR 功能用途不同，同一个游戏实例中只能安装一个。

## 单机版

仅适用于单人游戏。

1. 安装 Minecraft 1.20.1 和 Forge 47.3.0。
2. 打开分发包的 `prebuilt` 文件夹。
3. 将 `yokonex-gamehub-minecraft-1.1.1.jar` 复制到游戏目录的 `mods` 文件夹。
4. 在同一台电脑启动 Yokonex-Gamehub。
5. 打开 GameHub 的“游戏联动”，找到 Minecraft。
6. 启用 Minecraft，并为需要的事件配置指令。
7. 保持 GameHub 运行，再通过 Forge 启动 Minecraft。

## 局域网与专用服务器版

适用于“对局域网开放”的联机房间，以及独立运行的 Forge 专用服务器。

1. 所有参与联机的玩家都安装 Minecraft 1.20.1 和 Forge 47.3.0。
2. 打开分发包的 `prebuilt` 文件夹。
3. 将 `yokonex-gamehub-minecraft-server-1.1.1.jar` 放入房主、服务器和所有玩家的 `mods` 文件夹。
4. 局域网联机由房主正常进入存档，再选择“对局域网开放”；专用服务器则启动 Forge 服务端。
5. 每个需要设备联动的玩家，在自己的电脑上启动 GameHub。
6. 每个玩家在自己的 GameHub 中连接自己的蓝牙设备。
7. 每个玩家打开 GameHub 的“游戏联动”，启用 Minecraft，并配置事件指令。
8. 玩家保持自己的 GameHub 运行，再加入房间或服务器。

纯专用服务器机器不需要运行 GameHub，也不需要连接蓝牙设备。局域网房主本身也是玩家，需要设备联动时同样要运行 GameHub。

服务器版 Mod 会把事件定向发送给触发事件的玩家客户端，再由该玩家电脑上的 GameHub 控制该玩家自己的设备。所有玩家都需要安装同版本的服务器版 JAR，且不要同时安装单机版 JAR。某个玩家没有运行 GameHub 时，只会跳过该玩家的设备联动，不会影响房间、服务器或其他玩家。

## 如何确认连接正常

1. 在当前玩家自己电脑的 GameHub 中确认 Minecraft 联动已启用。
2. 进入游戏后触发一次“玩家受伤”或“破坏方块”事件。
3. 查看 GameHub 的 Minecraft 接收次数是否增加。
4. 如果没有增加，确认 GameHub 已先启动，并确认没有同时安装两个 JAR。

## 连接限制

- Mod 只通过 `ws://127.0.0.1:43002/v1/events` 连接本机 GameHub。
- 专用服务器版只把玩家事件发送给对应玩家，不会转发给其他玩家的 GameHub。
- Mod 不保存 UID、Token 或 IM 地址。
- Mod 不直接连接役次元 IM。
- GameHub 未运行时，游戏仍可正常运行，但事件不会触发设备。

## 源码构建

本地版：

```powershell
cd client_mod
.\gradlew.bat build
```

专用服务器版：

```powershell
cd server_mod
.\gradlew.bat build
```
