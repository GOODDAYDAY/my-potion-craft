<#
.SYNOPSIS
    构建 MagicShelf 插件并部署到游戏的 BepInEx\plugins\。

.DESCRIPTION
    1) 定位游戏根目录（可用 -GameDir 指定，否则自动从注册表/Steam 库文件查找）
    2) dotnet build -c Release -p:GameDir=...
    3) 除非指定 -NoDeploy，把 MagicShelf.dll 复制到 <游戏>\BepInEx\plugins\

.EXAMPLE
    pwsh -File build.ps1

.EXAMPLE
    pwsh -File build.ps1 -GameDir "D:\Steam\steamapps\common\Potion Craft" -NoDeploy
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
$pluginRoot = $PSScriptRoot

function Find-GamePath {
    $steamRoots = New-Object System.Collections.ArrayList

    try {
        $steamPath = (Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction Stop).SteamPath
        if ($steamPath) { [void]$steamRoots.Add($steamPath) }
    } catch { }

    if (${env:ProgramFiles(x86)}) { [void]$steamRoots.Add((Join-Path ${env:ProgramFiles(x86)} 'Steam')) }
    if ($env:ProgramFiles)        { [void]$steamRoots.Add((Join-Path $env:ProgramFiles 'Steam')) }

    foreach ($root in $steamRoots) {
        if (-not $root -or -not (Test-Path $root)) { continue }

        $libraries = New-Object System.Collections.ArrayList
        [void]$libraries.Add($root)

        $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
        if (Test-Path $vdf) {
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) {
                [void]$libraries.Add(($m.Groups[1].Value -replace '\\\\', '\'))
            }
        }

        foreach ($lib in $libraries) {
            $candidate = Join-Path $lib 'steamapps\common\Potion Craft'
            if (Test-Path (Join-Path $candidate 'Potion Craft.exe')) { return $candidate }
        }
    }
    return $null
}

if (-not $GameDir) {
    $GameDir = Find-GamePath
    if (-not $GameDir) {
        Write-Host '未能定位游戏目录，请用 -GameDir 指定。' -ForegroundColor Red
        exit 2
    }
    Write-Host "已自动定位游戏目录：$GameDir"
}

$scriptsDll = Join-Path $GameDir 'Potion Craft_Data\Managed\PotionCraft.Scripts.dll'
if (-not (Test-Path $scriptsDll)) {
    Write-Host "游戏目录里找不到 PotionCraft.Scripts.dll：$GameDir" -ForegroundColor Red
    exit 2
}

Write-Host ''
Write-Host '=== 构建 ===' -ForegroundColor Cyan
& dotnet build (Join-Path $pluginRoot 'MagicShelf.csproj') -c Release -p:GameDir="$GameDir" -v minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host '构建失败。' -ForegroundColor Red
    exit $LASTEXITCODE
}

$built = Join-Path $pluginRoot 'bin\Release\MagicShelf.dll'
if (-not (Test-Path $built)) {
    Write-Host "构建产物不存在：$built" -ForegroundColor Red
    exit 1
}

if ($NoDeploy) {
    Write-Host ''
    Write-Host "已构建（未部署）：$built" -ForegroundColor Green
    exit 0
}

$pluginsDir = Join-Path $GameDir 'BepInEx\plugins'
if (-not (Test-Path $pluginsDir)) {
    Write-Host "找不到 BepInEx\plugins 目录，请先安装 BepInEx：$pluginsDir" -ForegroundColor Red
    exit 2
}

Copy-Item $built (Join-Path $pluginsDir 'MagicShelf.dll') -Force
$hash = (Get-FileHash (Join-Path $pluginsDir 'MagicShelf.dll') -Algorithm SHA256).Hash

Write-Host ''
Write-Host '=== 部署完成 ===' -ForegroundColor Green
Write-Host "  目标：$(Join-Path $pluginsDir 'MagicShelf.dll')"
Write-Host "  SHA256：$hash"
Write-Host ''
Write-Host '启动游戏后可在 BepInEx\LogOutput.log 里搜索 MagicShelf 确认加载。'
