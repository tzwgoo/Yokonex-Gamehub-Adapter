param([string]$SdkPath = "")

$ErrorActionPreference = "Stop"
$candidates = @()
if ($SdkPath) {
  $candidates += Join-Path $SdkPath "SimConnect.dll"
  $candidates += Join-Path $SdkPath "SimConnect SDK\lib\SimConnect.dll"
}
$candidates += "C:\MSFS SDK\SimConnect SDK\lib\SimConnect.dll"
$candidates += "C:\MSFS 2024 SDK\SimConnect SDK\lib\SimConnect.dll"
$source = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $source) {
  throw "找不到 SimConnect.dll。请先从模拟器开发者模式安装 SDK，再运行：install.ps1 -SdkPath ""SDK目录"""
}
Copy-Item -LiteralPath $source -Destination (Join-Path $PSScriptRoot "SimConnect.dll") -Force
Write-Host "SimConnect 客户端已安装。"
