[CmdletBinding()]
param(
    [string]$GameDir = "",
    [switch]$SkipLaunch
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$modLoaderUrl = "https://github.com/GodotModding/godot-mod-loader/releases/download/v7.0.1/ModLoader-Self-Setup_7.0.1-WIN.zip"
$modLoaderSha256 = "BAF1613FE6691FDF7E5B6CCBD36DD23614A7CB2EEAB7FFECC2823515C60E1482"
$modFolderName = "cinian-buckshot_gamehub"

function Find-SteamRoots {
    $roots = [System.Collections.Generic.List[string]]::new()
    $registryKeys = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\Software\Valve\Steam",
        "HKLM:\Software\WOW6432Node\Valve\Steam"
    )
    foreach ($key in $registryKeys) {
        try {
            $value = Get-ItemProperty -LiteralPath $key
            foreach ($name in @("SteamPath", "InstallPath")) {
                $candidate = [string]$value.$name
                if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Container)) {
                    $roots.Add([System.IO.Path]::GetFullPath($candidate))
                }
            }
        } catch {
            # Steam 未写入该注册表位置时继续检查其它位置。
        }
    }
    $defaultSteam = Join-Path ${env:ProgramFiles(x86)} "Steam"
    if (Test-Path -LiteralPath $defaultSteam -PathType Container) {
        $roots.Add([System.IO.Path]::GetFullPath($defaultSteam))
    }

    foreach ($steamRoot in @($roots)) {
        $libraryFile = Join-Path $steamRoot "steamapps\libraryfolders.vdf"
        if (-not (Test-Path -LiteralPath $libraryFile -PathType Leaf)) {
            continue
        }
        $content = Get-Content -LiteralPath $libraryFile -Raw
        foreach ($match in [regex]::Matches($content, '"path"\s*"([^"]+)"')) {
            $path = $match.Groups[1].Value.Replace("\\", "\")
            if (Test-Path -LiteralPath $path -PathType Container) {
                $roots.Add([System.IO.Path]::GetFullPath($path))
            }
        }
    }
    return $roots | Select-Object -Unique
}

function Find-GameDirectory {
    foreach ($steamRoot in Find-SteamRoots) {
        $candidate = Join-Path $steamRoot "steamapps\common\Buckshot Roulette"
        if (Test-Path -LiteralPath (Join-Path $candidate "Buckshot Roulette.exe") -PathType Leaf) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }
    return ""
}

function Select-GameDirectory {
    Add-Type -AssemblyName System.Windows.Forms
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "选择包含 Buckshot Roulette.exe 的游戏目录"
    $dialog.ShowNewFolderButton = $false
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        return $dialog.SelectedPath
    }
    return ""
}

function Assert-ChildPath {
    param([string]$Parent, [string]$Child)
    $parentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd("\") + "\"
    $childFull = [System.IO.Path]::GetFullPath($Child)
    if (-not $childFull.StartsWith($parentFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "安装目标不在游戏目录内：$childFull"
    }
}

function Install-ModLoader {
    param([string]$TargetGameDir, [string]$BackupRoot)
    $loaderFile = Join-Path $TargetGameDir "addons\mod_loader\mod_loader.gd"
    if (Test-Path -LiteralPath $loaderFile -PathType Leaf) {
        Write-Host "[1/4] 已检测到 Godot Mod Loader"
        return
    }

    Write-Host "[1/4] 正在下载 Godot Mod Loader 7.0.1（约 24 MB）..."
    $tempBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd("\") + "\"
    $tempRoot = Join-Path $tempBase ("gamehub-buckshot-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $tempRoot | Out-Null
    try {
        $archivePath = Join-Path $tempRoot "mod-loader.zip"
        $extractPath = Join-Path $tempRoot "unpacked"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -Uri $modLoaderUrl -OutFile $archivePath
        $actualHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
        if ($actualHash -ne $modLoaderSha256) {
            throw "ModLoader 下载文件校验失败"
        }
        Expand-Archive -LiteralPath $archivePath -DestinationPath $extractPath
        $downloadedLoader = Join-Path $extractPath "addons\mod_loader\mod_loader.gd"
        if (-not (Test-Path -LiteralPath $downloadedLoader -PathType Leaf)) {
            throw "ModLoader 压缩包结构不正确"
        }

        $existingLoader = Join-Path $TargetGameDir "addons\mod_loader"
        if (Test-Path -LiteralPath $existingLoader) {
            $loaderBackup = Join-Path $BackupRoot "mod_loader"
            New-Item -ItemType Directory -Path (Split-Path $loaderBackup -Parent) -Force | Out-Null
            Move-Item -LiteralPath $existingLoader -Destination $loaderBackup
        }
        New-Item -ItemType Directory -Path (Join-Path $TargetGameDir "addons") -Force | Out-Null
        Copy-Item -Path (Join-Path $extractPath "addons\*") -Destination (Join-Path $TargetGameDir "addons") -Recurse -Force
    } finally {
        $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot)
        if ($resolvedTemp.StartsWith($tempBase, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedTemp)) {
            [System.IO.Directory]::Delete($resolvedTemp, $true)
        }
    }
}

function Patch-ModLoader {
    param([string]$TargetGameDir)
    Write-Host "[2/4] 正在检查 ModLoader 路径配置..."
    $storePath = Join-Path $TargetGameDir "addons\mod_loader\mod_loader_store.gd"
    if (-not (Test-Path -LiteralPath $storePath -PathType Leaf)) {
        throw "找不到 ModLoader 核心文件：$storePath"
    }
    $content = [System.IO.File]::ReadAllText($storePath)
    $oldLine = 'const UNPACKED_DIR := "res://mods-unpacked/"'
    $newLine = 'const UNPACKED_DIR := "./mods-unpacked/"'
    if ($content.Contains($oldLine)) {
        Copy-Item -LiteralPath $storePath -Destination ($storePath + ".gamehub.bak") -Force
        $utf8 = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($storePath, $content.Replace($oldLine, $newLine), $utf8)
    }
}

function Install-GameHubMod {
    param([string]$TargetGameDir, [string]$BackupRoot)
    Write-Host "[3/4] 正在安装恶魔轮盘 GameHub 联动..."
    $sourceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
    foreach ($required in @("manifest.json", "mod_main.gd", "gamehub.json", "extensions\event_bus.gd", "extensions\gamehub_bridge.gd")) {
        if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot $required) -PathType Leaf)) {
            throw "插件包缺少文件：$required"
        }
    }

    $modsRoot = Join-Path $TargetGameDir "mods-unpacked"
    $target = Join-Path $modsRoot $modFolderName
    Assert-ChildPath -Parent $TargetGameDir -Child $target
    New-Item -ItemType Directory -Path $modsRoot -Force | Out-Null
    if (Test-Path -LiteralPath $target) {
        $modBackup = Join-Path $BackupRoot $modFolderName
        New-Item -ItemType Directory -Path (Split-Path $modBackup -Parent) -Force | Out-Null
        Move-Item -LiteralPath $target -Destination $modBackup
    }
    New-Item -ItemType Directory -Path $target | Out-Null
    foreach ($name in @("manifest.json", "mod_main.gd", "gamehub.json", "USAGE.md", "BR_API.md", "extensions")) {
        $source = Join-Path $sourceRoot $name
        if (Test-Path -LiteralPath $source) {
            Copy-Item -LiteralPath $source -Destination $target -Recurse -Force
        }
    }
}

function Initialize-ModLoader {
    param([string]$TargetGameDir, [string]$GameExecutable)
    Write-Host "[4/4] 正在初始化 ModLoader..."
    $overridePath = Join-Path $TargetGameDir "override.cfg"
    if (Test-Path -LiteralPath $overridePath -PathType Leaf) {
        Write-Host "      ModLoader 已初始化"
        return
    }
    $process = Start-Process -FilePath $GameExecutable `
        -ArgumentList @("--script", "addons/mod_loader/mod_loader_setup.gd", "--setup-create-override-cfg", "--only-setup") `
        -WorkingDirectory $TargetGameDir -Wait -PassThru
    if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $overridePath -PathType Leaf)) {
        throw "ModLoader 初始化失败，请检查游戏目录和安全软件拦截记录"
    }
}

try {
    if (-not $GameDir) {
        $GameDir = Find-GameDirectory
    }
    if (-not $GameDir) {
        $GameDir = Select-GameDirectory
    }
    if (-not $GameDir) {
        throw "未选择游戏目录"
    }

    $GameDir = [System.IO.Path]::GetFullPath($GameDir)
    $gameExecutable = Join-Path $GameDir "Buckshot Roulette.exe"
    if (-not (Test-Path -LiteralPath $gameExecutable -PathType Leaf)) {
        throw "所选目录中没有 Buckshot Roulette.exe"
    }

    $backupRoot = Join-Path $GameDir ("gamehub-mod-backups\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    Assert-ChildPath -Parent $GameDir -Child $backupRoot
    Install-ModLoader -TargetGameDir $GameDir -BackupRoot $backupRoot
    Patch-ModLoader -TargetGameDir $GameDir
    Install-GameHubMod -TargetGameDir $GameDir -BackupRoot $backupRoot
    Initialize-ModLoader -TargetGameDir $GameDir -GameExecutable $gameExecutable

    Write-Host ""
    Write-Host "安装成功：$GameDir" -ForegroundColor Green
    Write-Host "请保持 GameHub 网关运行，并在插件中心开启恶魔轮盘联动。"
    if (-not $SkipLaunch) {
        Start-Process -FilePath $gameExecutable -WorkingDirectory $GameDir
    }
    exit 0
} catch {
    Write-Host ""
    Write-Host ("安装失败：" + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
