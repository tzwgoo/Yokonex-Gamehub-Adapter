param(
  [string]$GamePath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Euro Truck Simulator 2"
)

$ErrorActionPreference = "Stop"
$target = Join-Path $GamePath "bin\win_x64\plugins"
if (-not (Test-Path -LiteralPath $GamePath)) { throw "找不到游戏目录：$GamePath" }
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "scs-telemetry.dll") -Destination $target -Force
Write-Host "SCS 遥测插件已安装到 $target"
