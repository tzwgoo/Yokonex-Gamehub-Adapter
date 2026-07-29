param(
  [string]$GamePath = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Cult of the Lamb"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $GamePath)) { throw "找不到游戏目录：$GamePath" }
$bepinex = Join-Path $GamePath "BepInEx\core"
if (-not (Test-Path -LiteralPath (Join-Path $bepinex "BepInEx.dll"))) {
  throw "未安装 BepInExPack CultOfTheLamb 5.4.21。请先通过 Thunderstore/r2modman 安装。"
}
$managed = Join-Path $GamePath "Cult Of The Lamb_Data\Managed"
$compiler = @(
  "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
  "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw "找不到 Windows C# 编译器。" }

$references = @(
  (Join-Path $bepinex "BepInEx.dll"),
  (Join-Path $bepinex "0Harmony.dll"),
  (Join-Path $managed "UnityEngine.CoreModule.dll")
)
foreach ($reference in $references) {
  if (-not (Test-Path -LiteralPath $reference)) { throw "缺少编译依赖：$reference" }
}
$plugins = Join-Path $GamePath "BepInEx\plugins"
New-Item -ItemType Directory -Force -Path $plugins | Out-Null
$output = Join-Path $plugins "YokonexCultLink.dll"
& $compiler /nologo /target:library /optimize+ /out:$output /reference:$($references -join ",") (Join-Path $PSScriptRoot "cult_mod\YokonexCultLink.cs")
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output)) { throw "事件 Mod 编译失败。" }
Write-Host "咩咩启示录事件 Mod 已安装到 $output"
