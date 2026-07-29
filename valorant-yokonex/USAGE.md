# VALORANT 联动使用教程

## 前置条件

- Windows 10 或 Windows 11 64 位系统。
- 已安装 VALORANT、Riot Client 和最新版 Overwolf 客户端。
- 已登录 Overwolf 账号，并且该账号具备加载未打包或未发布扩展的权限。
- Yokonex-Gamehub 已安装，状态页显示“网关运行中”。
- 如需发送 IM 指令，先在“配置”页面填写并保存 IM 配置。
- 如需蓝牙反馈，先在“蓝牙”页面连接设备。
- 本适配器不需要 Node.js、Python 或 .NET 环境。

重要：当前提供的是 Overwolf 未打包扩展。没有开发权限的账号可能出现 `Unauthorized App`，这种情况需要先申请 Overwolf 开发权限；正式公开分发时应改用审核后的 OPK 应用。

## 第一步：配置联动事件

1. 启动 Yokonex-Gamehub。
2. 打开“联动”页面，找到“VALORANT”。
3. 点击“配置”。
4. 检查每个事件的 `commandId`。
5. 按需启用或禁用单个事件，并为需要蓝牙反馈的事件选择波形。
6. 保存配置。
7. 在卡片外层打开“启用游戏联动”开关。

## 第二步：加载 Overwolf 适配器

1. 先退出 VALORANT。
2. 在 VALORANT 卡片点击“打开 Mod”，网关会打开 `valorant-yokonex` 文件夹。
3. 登录 Overwolf 客户端。
4. 右键 Overwolf 托盘图标，依次打开“设置”→“关于”→“开发选项”。
5. 点击“加载未打包扩展”。
6. 选择刚才打开的 `valorant-yokonex` 根目录；该目录中必须直接包含 `manifest.json`。
7. 在 Overwolf 应用列表中确认 `Yokonex VALORANT Link` 已加载且处于启用状态。

## 第三步：启动游戏

1. 保持 Yokonex-Gamehub 运行。
2. 保持 Overwolf 运行，并确认适配器没有被禁用。
3. 通过 Riot Client 启动 VALORANT。
4. 进入一场对局，触发回合开始、击杀、死亡或爆能器事件。
5. 首次启动时等待 Overwolf 完成游戏事件组件更新。

## 第四步：确认联动

1. 返回网关“联动”页面。
2. 检查 VALORANT 卡片的事件数量和最后接收时间是否更新。
3. 如果事件已收到但蓝牙没有动作，检查该事件是否启用、是否选择波形，以及蓝牙设备是否在线。
4. 如果 IM 没有响应，检查对应 `commandId` 和“配置”页面中的 IM 设置。

## 常见问题

- 出现 `Unauthorized App`：当前 Overwolf 账号没有加载未发布扩展的权限，或账号尚未登录。
- 开发选项中看不到适配器：重新选择直接包含 `manifest.json` 的目录，不要选择它的上级目录。
- 完全没有事件：确认 Overwolf 已识别 VALORANT，并且适配器、网关和外层联动开关都已启用。
- 只有部分事件：Overwolf 能提供的事件会受游戏模式、GEP 版本和当前游戏版本影响；训练场或自定义模式可能缺少部分对局事件。
- 击杀归属不准确：隐藏玩家名称时，本地玩家识别可能受限。
- 更新网关后仍使用旧适配器：在 Overwolf 中删除旧扩展，再从网关重新打开并加载目录。
