# Apex Legends 联动使用教程

## 前置条件

- Windows 10 或 Windows 11 64 位系统。
- 已安装 Apex Legends 和最新版 Overwolf 客户端。
- 已登录具备未发布 App 加载权限的 Overwolf 开发者账号。
- Yokonex-Gamehub 状态页显示“网关运行中”。
- 本适配器不需要 Node.js、Python 或 .NET 环境。

正式公开使用前，需要完成 Overwolf App 提案审批和上架。申请说明见同目录的 `OVERWOLF-APPLICATION.md`。

## 第一步：配置联动事件

1. 启动 Yokonex-Gamehub。
2. 打开“联动”页面，找到“Apex Legends”。
3. 点击“配置”。
4. 检查每个事件的 `commandId`。
5. 按需启用单个事件并选择蓝牙波形。
6. 保存配置，并打开卡片外层的“启用游戏联动”开关。

## 第二步：加载 Overwolf 适配器

1. 退出 Apex Legends。
2. 在 Apex Legends 卡片点击“打开 Mod”。
3. 登录 Overwolf 客户端。
4. 打开“设置”→“关于”→“开发选项”。
5. 点击“加载未打包扩展”。
6. 选择直接包含 `manifest.json` 的 `apex-yokonex` 文件夹。
7. 确认 `Yokonex Apex Link` 已启用。

## 第三步：启动和验证

1. 保持 Yokonex-Gamehub 与 Overwolf 运行。
2. 启动 Apex Legends 并进入一场对局。
3. 触发伤害、击倒、击杀、倒地或复活等事件。
4. 返回“联动”页面，检查事件数量和最后接收时间。
5. 如果蓝牙没有动作，检查事件开关、波形映射和设备连接状态。
6. 如果 IM 没有响应，在“设备连接”页面检查 IM 连接。

## 安全边界

- 只读取 Overwolf GEP 提供的事件。
- 不读取游戏内存，不注入 DLL，不操作鼠标、键盘或手柄。
- 不提供压枪、宏、瞄准、自动操作或任何竞争优势。

## 常见问题

- `Unauthorized App`：账号尚未获得 Overwolf App 提案白名单。
- 找不到开发选项：先登录正确账号，并确认提案已通过。
- 没有事件：确认 Overwolf 已识别 Apex Legends，且 GEP 组件为最新版本。
- 部分事件缺失：不同模式和 GEP 版本提供的事件可能不同。
- 网关连接失败：确认 Yokonex-Gamehub 正在监听 `127.0.0.1:43002`。
