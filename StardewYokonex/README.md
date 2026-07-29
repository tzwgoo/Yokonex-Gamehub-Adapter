# Yokonex Stardew Link

独立 SMAPI Mod，覆盖存档载入、每日开始/结束、获得物品、技能升级、地点切换、玩家加入和离开。

## 用户安装

在网关“联动”页面点击“安装 SMAPI Mod”，选择星露谷物语目录即可。用户电脑不需要安装 .NET SDK。

## 发布构建

需要 Stardew Valley 1.6、SMAPI 4 和 .NET 6 SDK：

```powershell
powershell -ExecutionPolicy Bypass -File ..\..\scripts\build-stardew-mod.ps1 -GamePath "你的星露谷安装目录"
```

脚本会将 DLL 放进 `prebuilt`。桌面程序打包前会检查这个文件，避免发布缺少 Mod 的安装包。

Mod 不直接保存 IM Token。事件回调只入队，后台连接 `ws://127.0.0.1:43002/stardew`。
