param([string]$ModsPath = "$env:APPDATA\Balatro\Mods")

$ErrorActionPreference = "Stop"
$target = Join-Path $ModsPath "YokonexGameHub"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "balatro_mod\YokonexGameHub.lua") -Destination $target -Force
Write-Host "Balatro Mod 已安装到 $target"
Write-Host "请确认 Lovely 和 Steamodded 1.0+ 已安装。"
