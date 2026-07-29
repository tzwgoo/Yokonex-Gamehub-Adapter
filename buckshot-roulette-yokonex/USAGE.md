# 恶魔轮盘联动

将恶魔轮盘的对局、射击、生命值、弹药和物品事件发送到 GameHub，并触发已配置的 IM 指令与设备波形。

## 安装

1. 在 GameHub 插件中心安装本插件，打开插件目录。
2. 双击 `一键安装.bat`。
3. 脚本会自动识别 Steam 游戏目录；首次安装会下载并校验官方 Godot Mod Loader 7.0.1。
4. 安装完成后保持 GameHub 网关运行，并在插件中心开启恶魔轮盘联动。

自动识别失败时，脚本会让你选择包含 `Buckshot Roulette.exe` 的目录。更新已有版本时，旧文件会备份到游戏目录的 `gamehub-mod-backups`。

也可以手动把插件目录复制到游戏的 `mods-unpacked`；目录中应能直接看到 `manifest.json`、`mod_main.gd` 和 `extensions`。

## 配置

在 GameHub 的“插件中心”中开启恶魔轮盘联动，再进入“配置”设置每个事件的 commandId 和设备波形。Mod 每 5 秒同步一次配置。

GameHub 使用非默认端口时，修改插件目录中的 `gamehub.json`：

```json
{
  "gateway_url": "http://127.0.0.1:43002"
}
```

## 验证

启动游戏并进入一局。扣动扳机后，GameHub 日志应出现：

```text
source=buckshot_roulette event=buckshot.shoot
```

若没有事件，请检查 GameHub 网关是否运行、插件总开关是否开启，以及 `gamehub.json` 端口是否一致。

## 说明

- 本插件内含 BR-API 事件钩子，不需要再安装同名 API Mod。
- `BR_API.md` 列出了全部事件字段。
- 卸载时删除游戏 `mods-unpacked` 下对应目录，并在 GameHub 中移除插件。
