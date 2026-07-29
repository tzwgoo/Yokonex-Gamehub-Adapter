# 炉石传说联动

## 使用

1. 启动炉石传说并确认游戏已生成 `Power.log`。
2. 在 GameHub“联动”页面启用炉石传说。
3. 配置需要的事件和冷却时间，再开始一局游戏。

GameHub 会自动查找 `%LOCALAPPDATA%\Blizzard\Hearthstone\Logs` 和常见游戏安装目录。
无法识别自定义路径时，将环境变量 `HEARTHSTONE_POWER_LOG` 设置为 `Power.log` 的完整路径。

如果本机没有日志配置，适配器会在
`%LOCALAPPDATA%\Blizzard\Hearthstone\log.config` 创建原生 Power 日志配置。创建后需要重新启动一次游戏。
已有配置不会被修改。

## 安全说明

- 只读取炉石传说输出的本地日志。
- 不读取游戏内存，不注入进程，不操作鼠标和键盘。
- 只上报本地玩家的出牌、英雄伤害和对局结果。
