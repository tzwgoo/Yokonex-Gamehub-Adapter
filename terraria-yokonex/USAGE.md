# 泰拉瑞亚联动使用教程

## 环境要求

- Terraria 1.4.4.9
- tModLoader 1.4.4 stable
- Yokonex-Gamehub 保持运行

## 安装

1. 在插件中心找到“泰拉瑞亚”。
2. 点击“安装 tModLoader Mod”。
3. 启动 tModLoader，在“管理模组”中启用 `Terraria 役次元 IM 联动`。
4. 进入世界后，Mod 会自动连接本机 Yokonex-Gamehub。

默认安装目录是：

`文档\My Games\Terraria\tModLoader\Mods\Terraria-YOKONEX.tmod`

## 配置

在 Yokonex-Gamehub 的“插件中心 → 泰拉瑞亚 → 配置”中设置：

- 游戏联动总开关
- 19 类事件的独立开关
- 每个事件对应的 commandId
- 每个 commandId 对应的蓝牙设备与波形

Mod 每 5 秒同步一次 GameHub 配置。关闭单个事件后，Mod 不再发送该事件。

## 测试

进入游戏世界后可执行：

`/yokonex trigger player_hurt`

GameHub 日志出现 `source=terraria event=player_hurt` 即表示连接正常。

## 注意

- 新版本不再直接登录公网 IM。
- IM 登录、蓝牙连接和波形执行都由 Yokonex-Gamehub 统一处理。
- 原项目源码位于 `https://github.com/tzwgoo/Terraria-YOKONEX`。
