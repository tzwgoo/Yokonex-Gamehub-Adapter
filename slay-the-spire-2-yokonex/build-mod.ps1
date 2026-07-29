param(
  [string]$GamePath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Slay the Spire 2"
)

$ErrorActionPreference = "Stop"
$managed = Join-Path $GamePath "data_sts2_windows_x86_64"
foreach ($dependency in @("sts2.dll", "GodotSharp.dll", "0Harmony.dll")) {
  if (-not (Test-Path -LiteralPath (Join-Path $managed $dependency))) {
    throw "Missing game assembly: $dependency"
  }
}

$project = Join-Path $PSScriptRoot "sts2_event_mod\YokonexSTS2Events.csproj"
dotnet build $project -c Release -p:STS2GameDir="$managed" --nologo
if ($LASTEXITCODE -ne 0) { throw "STS2 event mod build failed." }

$prebuilt = Join-Path $PSScriptRoot "prebuilt"
New-Item -ItemType Directory -Force -Path $prebuilt | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "sts2_event_mod\bin\Release\net9.0\Yokonex-STS2-Events.dll") -Destination $prebuilt -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "sts2_event_mod\Yokonex-STS2-Events.json") -Destination $prebuilt -Force
Write-Host "STS2 event mod generated at $prebuilt"
