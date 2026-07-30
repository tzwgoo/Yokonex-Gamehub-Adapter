# Yokonex GameHub Adapter

Yokonex GameHub 的游戏适配器集合。每个目录对应一个游戏，通过官方接口、游戏 Mod、日志或本机事件把游戏状态发送到 GameHub。

## 支持的游戏

| 游戏 | 适配器 |
| --- | --- |
| Apex Legends | [`apex-yokonex`](./apex-yokonex/) |
| American Truck Simulator | [`ats-yokonex`](./ats-yokonex/) |
| Balatro | [`balatro-yokonex`](./balatro-yokonex/) |
| Buckshot Roulette | [`buckshot-roulette-yokonex`](./buckshot-roulette-yokonex/) |
| Counter-Strike 2 | [`cs2-yokonex`](./cs2-yokonex/) |
| Cult of the Lamb | [`cult-of-the-lamb-yokonex`](./cult-of-the-lamb-yokonex/) |
| Dota 2 | [`dota2-yokonex`](./dota2-yokonex/) |
| Don't Starve Together | [`dst-yokonex`](./dst-yokonex/) |
| Euro Truck Simulator 2 | [`ets2-yokonex`](./ets2-yokonex/) |
| EA SPORTS F1 24/25 | [`f1-yokonex`](./f1-yokonex/) |
| Factorio | [`factorio-yokonex`](./factorio-yokonex/) |
| Hearthstone | [`hearthstone-yokonex`](./hearthstone-yokonex/) |
| The Binding of Isaac: Repentance | [`isaac-yokonex`](./isaac-yokonex/) |
| League of Legends | [`lol-yokonex`](./lol-yokonex/) |
| Minecraft | [`minecraft-yokonex`](./minecraft-yokonex/) |
| Microsoft Flight Simulator | [`msfs-yokonex`](./msfs-yokonex/) |
| Secret Flasher Manaka | [`secret-flasher-manaka-yokonex`](./secret-flasher-manaka-yokonex/) |
| StarCraft II | [`sc2-yokonex`](./sc2-yokonex/) |
| Slay the Spire 2 | [`slay-the-spire-2-yokonex`](./slay-the-spire-2-yokonex/) |
| Slay the Spire | [`slay-the-spire-yokonex`](./slay-the-spire-yokonex/) |
| Stardew Valley | [`StardewYokonex`](./StardewYokonex/) |
| Terraria | [`terraria-yokonex`](./terraria-yokonex/) |
| VALORANT | [`valorant-yokonex`](./valorant-yokonex/) |
| VRChat | [`vrchat-yokonex`](./vrchat-yokonex/) |
| World of Warcraft | [`wow-yokonex`](./wow-yokonex/) |

## 目录说明

- 每个适配器的 `README.md` 介绍用途。
- `USAGE.md` 提供安装和使用方法。
- `manifest.json` 是 GameHub 读取的适配器清单。
- `_shared` 存放多个适配器共用的代码。

## 使用

先打开对应游戏目录，按其中的 `README.md` 和 `USAGE.md` 操作。适配器默认只连接本机运行的 Yokonex GameHub。
