# Buckshot Roulette Mod API (BR-API) 使用文档

## 概述

BR-API 为 Buckshot Roulette (Godot 4.1.1) 提供事件总线和即时读取接口。Mod 开发者可订阅游戏事件、读取游戏状态。

**技术栈**: Godot Mod Loader v7.0.1 | GodotSteam | Godot 4.1.1

---

## 快速开始

```gdscript
# 获取事件总线
var eb = Engine.get_meta("br_api_bus", null)
if not eb:
    return

# 订阅事件
eb.on(eb.EVT_SHOOT, func(data):
    print("开枪！", data.source, "→", data.apply)
)
# 参数: eb.on(<事件签名>,<事件触发后调用的函数>)

# 即时读取
var hp = eb.player_hp()
var inv = eb.inventory()
```

### 名称声明
* 玩家: "Player"
* 庄家(AI): "Dealer"
* 无尽模式: 通常指"加倍还是放弃"模式
* "blank"："实弹"
* "live": 指空包弹
---

## 事件参考

### 通用事件

#### `game_start` — 游戏开始事件

玩家完成生死状签名时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `name` | String | 玩家签名（仅字母） |
| `infinite` | bool | 是否无尽模式 |

```gdscript
eb.on(eb.EVT_GAME_START, func(data):
    # data: {"name": "cxk", "infinite": false}
    print("玩家签名: ", data.name)
)
```

---

### 轮次事件

#### `round_start` — 轮次开始事件

摄像机移动到小屏幕、显示血量时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `round` | int | 第几轮（1~3） |
| `health` | int | 该轮初始血量 |
| `infinite` | bool | 是否无尽模式 |

```gdscript
eb.on(eb.EVT_ROUND_START, func(data):
    # data: {"round": 2, "health": 4, "infinite": false}
    print("第", data.round, "轮开始，血量:", data.health)
)
```

#### `round_won` — 轮次胜利事件

玩家赢得本轮时触发（庄家血量归零）。无参数。

```gdscript
eb.on(eb.EVT_ROUND_WON, func(_data):
    print("玩家赢得本轮！")
)
```

---

### 生命值事件

#### `health_changed` — 生命值更新事件

玩家或庄家血量发生变化时触发（射击/香烟/过期药品）。

| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | int | 变化后的血量 |
| `type` | String | `"Player"` 或 `"Dealer"` |
| `infinite` | bool | 是否无尽模式 |

```gdscript
eb.on(eb.EVT_HEALTH_CHANGED, func(data):
    # data: {"value": 2, "type": "Player", "infinite": false}
    if data.type == "Player":
        print("玩家血量变为: ", data.value)
)
```

#### `no_regeneration` — 不可再生状态事件

第三轮血量降至 1 或 2、除颤仪被切断时触发。无参数。

```gdscript
eb.on(eb.EVT_NO_REGEN, func(_data):
    print("血量无法恢复！")
)
```

---

### 射击事件

#### `shoot` — 使用枪械事件

玩家或庄家扣动扳机时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `source` | String | 谁开枪（`"Player"` / `"Dealer"`） |
| `apply` | String | 对谁开枪（`"Player"` / `"Dealer"`） |
| `inFire` | bool | 实弹 `true` / 空包弹 `false` |
| `damage` | int | 实际伤害值。如果开枪前使用小刀,damage为2,如果是空包弹则为0,否则为1。 |
| `bulletQuantity` | int | 开枪后剩余弹药数 |

```gdscript
eb.on(eb.EVT_SHOOT, func(data):
    # data: {"source":"Player","apply":"Dealer","inFire":true,"damage":1,"bulletQuantity":3}
    if data.inFire:
        print(data.source, "对", data.apply, "射出一发实弹！")
    else:
        print("空包弹！剩余:", data.bulletQuantity)
)
```

---

### 弹药事件

#### `ammo_updated` — 弹药总数量更新事件

新一轮弹药展示时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `value` | int | 弹药总数 |
| `live` | int | 实弹数量 |
| `blank` | int | 空包弹数量 |

```gdscript
eb.on(eb.EVT_AMMO_UPDATED, func(data):
    # data: {"value": 7, "live": 4, "blank": 3}
    print("弹药: 共", data.value, "发 (实弹", data.live, "空包弹", data.blank, ")")
)
```

---

### 物品事件

#### `item_placed` — 获得并放置道具事件

玩家从物品箱取出物品放到桌面时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `type` | String | 物品类型 |
| `slot` | int | 放置的格子编号 |

```gdscript
eb.on(eb.EVT_ITEM_PLACED, func(data):
    # data: {"type": "beer", "slot": 2}
    print("放置物品: ", data.type, " 在格子 ", data.slot)
)
```

#### `item_used` — 物品使用事件

玩家使用桌上物品时触发（含肾上腺素偷庄家物品）。

| 参数 | 类型 | 说明 |
|------|------|------|
| `type` | String | 物品类型 |
| `slot` | int | 使用前所在格子 |
| `fromDealer` | bool | 是否来自庄家 |

```gdscript
eb.on(eb.EVT_ITEM_USED, func(data):
    # data: {"type":"beer","slot":1,"fromDealer":false}
    if data.fromDealer:
        print("玩家偷用了庄家的", data.type)
    else:
        print("玩家使用了", data.type)
)
```

#### `item_stolen` — 道具被使用事件

庄家用肾上腺素偷走玩家物品并立即使用时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `type` | String | 被偷走的物品类型 |
| `slot` | int | 被偷前所在格子 |

```gdscript
eb.on(eb.EVT_ITEM_STOLEN, func(data):
    # data: {"type": "beer", "slot": 3}
    print("庄家偷走了格子", data.slot, "的", data.type)
)
```

#### `items_cleared` — 物品刷新事件

新轮次开始、桌面物品被清空时触发。无参数。

```gdscript
eb.on(eb.EVT_ITEMS_CLEARED, func(_data):
    print("桌面物品已清空")
)
```

#### `item_box_opened` — 物品箱打开事件

物品箱送上桌子并打开时触发。无参数。

```gdscript
eb.on(eb.EVT_ITEM_BOX_OPENED, func(_data):
    print("物品箱已打开")
)
```

#### `item_box_closed` — 物品箱关闭事件

玩家取完物品、物品箱关闭收走时触发。无参数。

```gdscript
eb.on(eb.EVT_ITEM_BOX_CLOSED, func(_data):
    print("物品箱已关闭")
)
```

---

### 特定物品事件

#### `magnifying_glass_used` — 放大镜使用事件

| 参数 | 类型 | 说明 |
|------|------|------|
| `type` | String | 看到的子弹类型（`"blank"` / `"live"`） |

```gdscript
eb.on(eb.EVT_MAGNIFYING_GLASS_USED, func(data):
    # data: {"type": "live"}
    print("放大镜显示: ", "实弹" if data.type == "live" else "空包弹")
)
```

#### `beer_used` — 啤酒使用事件

| 参数 | 类型 | 说明 |
|------|------|------|
| `type` | String | 弹出的子弹类型（`"blank"` / `"live"`） |
| `bulletQuantity` | int | 使用后剩余弹药数 |
| `source` | String | 谁使用（`"Player"` / `"Dealer"`） |

```gdscript
eb.on(eb.EVT_BEER_USED, func(data):
    # data: {"type":"blank","bulletQuantity":2,"source":"Player"}
    print(data.source, "用啤酒弹出了", "空包弹" if data.type == "blank" else "实弹")
)
```

#### `burner_phone_used` — 电话使用事件

| 参数 | 类型 | 说明 |
|------|------|------|
| `slot` | int | 预言的第几发子弹（1~7）；`0` 表示无有效预言 |
| `type` | String | 预言子弹类型（`"blank"` / `"live"`，如果预言无有效预言则为`"none"`） |

```gdscript
eb.on(eb.EVT_BURNER_PHONE_USED, func(data):
    # data: {"slot": 3, "type": "live"}
    # 无预言: {"slot": 0, "type": "none"}
    if data.slot == 0:
        print("电话：真遗憾，只剩一发")
    else:
        print("电话预言: 第", data.slot, "发是", "实弹" if data.type == "live" else "空包弹")
)
```

#### `medicine_used` — 药品使用事件

| 参数 | 类型 | 说明 |
|------|------|------|
| `apply` | bool | 恢复血量 `true` / 减少血量 `false` |

```gdscript
eb.on(eb.EVT_MEDICINE_USED, func(data):
    # data: {"apply": true}
    print("过期药品效果: ", "恢复" if data.apply else "减少", "血量")
)
```

---

### 手铐事件

#### `handcuffs_applied` — 被使用手铐事件

庄家对玩家使用手铐时触发。无参数。

```gdscript
eb.on(eb.EVT_HANDCUFFS_APPLIED, func(_data):
    print("玩家被铐住了！")
)
```

#### `handcuffs_removed` — 手铐解开事件

玩家手铐被解开时触发（回合切换或中场挣脱）。无参数。

```gdscript
eb.on(eb.EVT_HANDCUFFS_REMOVED, func(_data):
    print("手铐已解开")
)
```

> 请注意: 通常情况下游戏结束时不会触发手铐解开事件。
> 如果触发了"被使用手铐事件"，当手铐解开之前游戏就已经结束（未触发解开手铐的动画），会导致游戏结束后，手铐解开事件不会被触发。
> 请注意: 该事件还无法与动画同步。当庄家拿起手铐的那一刻，就会触发这个事件，而不是玩家被铐上之后触发。

---

### 游戏结算事件

#### `game_won` — 游戏胜利结算事件

玩家获胜、游戏结算时触发。无参数。

```gdscript
eb.on(eb.EVT_GAME_WON, func(_data):
    print("游戏胜利！")
)
```

#### `game_lost` — 游戏失败结算事件

玩家失败、游戏结算时触发。无参数。

```gdscript
eb.on(eb.EVT_GAME_LOST, func(_data):
    print("游戏失败……")
)
```

---

### 无尽模式事件

#### `score_updated` — 分数更新事件

第三轮获胜后、显示分数动画结束时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `score` | int | 当前分数 |

```gdscript
eb.on(eb.EVT_SCORE_UPDATED, func(data):
    # data: {"score": 167521}
    print("当前分数: ", data.score)
)
```

#### `double_or_nothing` — 加倍还是放弃交互事件

玩家选择"加倍"或"放弃"时触发。

| 参数 | 类型 | 说明 |
|------|------|------|
| `select` | bool | `true` = 加倍，`false` = 放弃 |

```gdscript
eb.on(eb.EVT_DOUBLE_OR_NOTHING, func(data):
    # data: {"select": true}
    print("玩家选择了: ", "加倍" if data.select else "放弃")
)
```

---

## 即时读取 API

### `inventory()` — 读取玩家物品栏

```gdscript
var items = eb.inventory()
# 返回: [{"slot": 1, "type": "beer"}, {"slot": 3, "type": "handcuffs"}]
for item in items:
    print("格子 ", item.slot, ": ", item.type)
```

### `player_hp()` — 读取玩家血量

```gdscript
var hp = eb.player_hp()
# 返回: {"value": 3, "regeneration": true}
print("玩家血量: ", hp.value)
if not hp.regeneration:
    print("注意：血量无法恢复")
```

### `round_info()` — 读取轮次信息

```gdscript
var info = eb.round_info()
# 返回: {"round": 2, "health": 4, "infinite": false}
print("第", info.round, "轮, 初始血量: ", info.health)
```

---

## 完整事件索引

| 事件常量 | 事件名 | 中文名称 | 返回的数据 |
|----------|--------|----------|------|
| `EVT_GAME_LAUNCH` | `game_launch` | 游戏启动/重启事件 | `{}` |
| `EVT_GAME_START` | `game_start` | 游戏开始事件（签署生死状） | `{name, infinite}` |
| `EVT_ROUND_START` | `round_start` | 轮次开始事件 | `{round, health, infinite}` |
| `EVT_ROUND_WON` | `round_won` | 轮次胜利事件 | `{}` |
| `EVT_HEALTH_CHANGED` | `health_changed` | 生命值更新事件 | `{value, type, infinite}` |
| `EVT_NO_REGEN` | `no_regeneration` | 不可再生状态事件 | `{}` |
| `EVT_SHOOT` | `shoot` | 使用枪械事件 | `{source, apply, inFire, damage, bulletQuantity}` |
| `EVT_AMMO_UPDATED` | `ammo_updated` | 弹药总数量更新事件 | `{value, live, blank}` |
| `EVT_ITEM_PLACED` | `item_placed` | 获得并放置道具事件 | `{type, slot}` |
| `EVT_ITEM_USED` | `item_used` | 物品使用事件 | `{type, slot, fromDealer}` |
| `EVT_ITEM_STOLEN` | `item_stolen` | 道具被偷取事件 | `{type, slot}` |
| `EVT_ITEMS_CLEARED` | `items_cleared` | 物品刷新事件 | `{}` |
| `EVT_ITEM_BOX_OPENED` | `item_box_opened` | 物品箱打开事件 | `{}` |
| `EVT_ITEM_BOX_CLOSED` | `item_box_closed` | 物品箱关闭事件 | `{}` |
| `EVT_MAGNIFYING_GLASS_USED` | `magnifying_glass_used` | 放大镜使用事件 | `{type}` |
| `EVT_BEER_USED` | `beer_used` | 啤酒使用事件 | `{type, bulletQuantity, source}` |
| `EVT_BURNER_PHONE_USED` | `burner_phone_used` | 电话使用事件 | `{slot, type}` |
| `EVT_MEDICINE_USED` | `medicine_used` | 药品使用事件 | `{apply}` |
| `EVT_HANDCUFFS_APPLIED` | `handcuffs_applied` | 被使用手铐事件 | `{}` |
| `EVT_HANDCUFFS_REMOVED` | `handcuffs_removed` | 手铐解开事件 | `{}` |
| `EVT_GAME_WON` | `game_won` | 游戏胜利结算事件 | `{}` |
| `EVT_GAME_LOST` | `game_lost` | 游戏失败结算事件 | `{}` |
| `EVT_SCORE_UPDATED` | `score_updated` | 分数更新事件 | `{score}` |
| `EVT_DOUBLE_OR_NOTHING` | `double_or_nothing` | 加倍还是放弃交互事件 | `{select}` |
| `EVT_GAME_CLOSE` | `game_close` | 游戏关闭/主动退出事件 | `{}` |

---

## 物品类型参考

| 类型字符串 | 物品名称 |
|-----------|---------|
| `"beer"` | 啤酒 |
| `"cigarettes"` | 香烟 |
| `"handcuffs"` | 手铐 |
| `"handsaw"` | 小刀 |
| `"magnifying glass"` | 放大镜 |
| `"burner phone"` | 电话 |
| `"expired medicine"` | 过期药品 |
| `"inverter"` | 反转器 |
| `"adrenaline"` | 肾上腺素 |
