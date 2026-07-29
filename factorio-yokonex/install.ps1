param([string]$ModsPath = "$env:APPDATA\Factorio\mods")

$ErrorActionPreference = "Stop"
$source = Join-Path $PSScriptRoot "factorio_mod"
$target = Join-Path $ModsPath "yokonex_factorio_events_1.0.0"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $source "info.json") -Destination $target -Force
Copy-Item -LiteralPath (Join-Path $source "control.lua") -Destination $target -Force
Write-Host "Factorio Mod 已安装到 $target"
Write-Host "请给 Factorio 启动参数加入 --enable-lua-udp"
