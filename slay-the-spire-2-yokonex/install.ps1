param(
  [string]$GamePath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $GamePath)) { throw "Game directory not found: $GamePath" }
$source = Join-Path $PSScriptRoot "prebuilt"
$target = Join-Path $GamePath "mods\Yokonex-STS2-Events"
foreach ($file in @("Yokonex-STS2-Events.dll", "Yokonex-STS2-Events.json")) {
  if (-not (Test-Path -LiteralPath (Join-Path $source $file))) { throw "Missing prebuilt file: $file" }
}
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath (Join-Path $source "Yokonex-STS2-Events.dll") -Destination $target -Force
Copy-Item -LiteralPath (Join-Path $source "Yokonex-STS2-Events.json") -Destination $target -Force
Write-Host "STS2 event mod installed at $target"
