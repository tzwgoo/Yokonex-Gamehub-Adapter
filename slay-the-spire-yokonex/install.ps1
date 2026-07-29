param(
  [string]$GamePath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\SlayTheSpire"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $GamePath)) { throw "找不到游戏目录：$GamePath" }
$mods = Join-Path $GamePath "mods"
New-Item -ItemType Directory -Force -Path $mods | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "CommunicationMod.jar") -Destination $mods -Force

$escapedRoot = $PSScriptRoot.Replace("'", "''")
$script = "& python '$escapedRoot\bridge.py' --stdio"
$encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($script))
$configDir = Join-Path $env:LOCALAPPDATA "ModTheSpire\CommunicationMod"
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
@"
command=powershell -NoProfile -EncodedCommand $encoded
runAtGameStart=true
verbose=false
"@ | Set-Content -LiteralPath (Join-Path $configDir "config.properties") -Encoding ASCII
Write-Host "CommunicationMod 已安装并连接到 GameHub 桥接器。"
