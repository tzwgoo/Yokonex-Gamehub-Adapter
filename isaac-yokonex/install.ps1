param(
    [string]$GameDir = ""
)

$ErrorActionPreference = "Stop"
$adapterDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $adapterDir "isaac_mod"

if (-not $GameDir) {
    $steamPath = (Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($steamPath) {
        $candidate = Join-Path $steamPath "steamapps\common\The Binding of Isaac Rebirth"
        if (Test-Path -LiteralPath $candidate) {
            $GameDir = $candidate
        }
    }
}

if (-not $GameDir -or -not (Test-Path -LiteralPath $GameDir)) {
    throw "Isaac game directory was not found. Pass it with -GameDir."
}

$resolvedGameDir = (Resolve-Path -LiteralPath $GameDir).Path
$targetRoot = Join-Path $resolvedGameDir "mods"
$targetDir = Join-Path $targetRoot "yokonex-isaac-events"
New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

# Only replace this adapter's own folder.
$resolvedTargetRoot = (Resolve-Path -LiteralPath $targetRoot).Path
$fullTargetDir = [System.IO.Path]::GetFullPath($targetDir)
if (-not $fullTargetDir.StartsWith($resolvedTargetRoot + [System.IO.Path]::DirectorySeparatorChar)) {
    throw "Resolved mod target is outside the game mods directory."
}
if (Test-Path -LiteralPath $targetDir) {
    Remove-Item -LiteralPath $targetDir -Recurse -Force
}
Copy-Item -LiteralPath $sourceDir -Destination $targetDir -Recurse

Write-Host "Installed: $targetDir"
Write-Host "Enable Yokonex GameHub Link in the in-game Mods menu."
