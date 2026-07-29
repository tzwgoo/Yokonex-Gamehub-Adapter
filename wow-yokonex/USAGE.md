# 魔兽世界联动

## 安装

1. 将 `wow_addon/YokonexGameHub` 复制到游戏版本目录下的 `Interface/AddOns/`。
2. 启动游戏，在角色选择页面启用 `Yokonex GameHub`。
3. 在 GameHub“联动”页面启用魔兽世界，并配置需要的事件。

插件登录后只调用游戏自带的 `LoggingCombat(true)`，战斗日志写入
`Logs/WoWCombatLog.txt`。GameHub 只读取新增内容。

支持正式服、经典服、经典怀旧服和周年服的常见目录。自定义安装位置无法自动识别时，
将环境变量 `WOW_COMBAT_LOG` 设置为 `WoWCombatLog.txt` 的完整路径。

## 安全说明

- 不读取游戏内存，不注入进程。
- 不发送按键，不改变角色操作。
- 高频伤害、治疗和施法事件建议配置较长冷却时间。
