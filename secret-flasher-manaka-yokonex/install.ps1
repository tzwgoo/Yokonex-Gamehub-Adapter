param(
    [string]$GameDir = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    $GameDir = Read-Host "请输入包含 SecretFlasherManaka.exe 的游戏目录"
}

$GameDir = $GameDir.Trim().Trim('"')
$gameExecutable = Join-Path $GameDir "SecretFlasherManaka.exe"
$bepInExRoot = Join-Path $GameDir "BepInEx"
if (-not (Test-Path -LiteralPath $gameExecutable -PathType Leaf)) {
    throw "游戏目录不正确：未找到 SecretFlasherManaka.exe"
}
if (-not (Test-Path -LiteralPath $bepInExRoot -PathType Container)) {
    throw "游戏尚未安装 BepInEx 6 IL2CPP"
}

$sourceDir = Join-Path $PSScriptRoot "prebuilt"
$targetDir = Join-Path $bepInExRoot "plugins\ManakaLinkYokonex"
$pluginFiles = @(
    "ManakaLinkYokonex.dll",
    "ManakaLinkYokonex.deps.json",
    "Microsoft.Windows.SDK.NET.dll",
    "WinRT.Runtime.dll"
)

# 只覆盖本 Mod 的运行文件，保留用户已有配置和其他 BepInEx 插件。
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
foreach ($fileName in $pluginFiles) {
    $sourcePath = Join-Path $sourceDir $fileName
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "市场包缺少运行文件：$fileName"
    }
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $targetDir $fileName) -Force
}

Write-Host "安装完成：$targetDir" -ForegroundColor Green
