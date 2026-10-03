<#
.SYNOPSIS
    只读校验 Potion Craft 的 BepInEx + mod 安装状态。

.DESCRIPTION
    逐项检查框架文件与插件 dll 是否存在、SHA256 是否与 mods/manifest.md 记录一致，
    并列出不在清单里的额外 dll。

    本脚本只读取文件，不会修改、删除、下载任何内容。
    兼容 Windows PowerShell 5.1 与 PowerShell 7+。

.NOTES
    ⚠️ 本文件必须保存为 **UTF-8 with BOM**。
    脚本里有中文输出，而 Windows PowerShell 5.1 对**没有 BOM** 的 .ps1 会按系统 ANSI
    代码页（中文系统是 GBK）解析，直接报"语句块缺少右 }"之类的语法错。
    用编辑器改完请在保存时选择"UTF-8 带 BOM"，或执行：
        $t = [IO.File]::ReadAllText($p, (New-Object Text.UTF8Encoding($false)))
        [IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($true)))

.PARAMETER GamePath
    游戏根目录（含 Potion Craft.exe 的那一层）。省略时尝试自动定位 Steam 安装位置。

.EXAMPLE
    pwsh -File scripts/verify-install.ps1 -GamePath "D:\Steam\steamapps\common\Potion Craft"

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts/verify-install.ps1
#>
[CmdletBinding()]
param(
    [string]$GamePath
)

# ---- 期望值（与 mods/manifest.md 保持一致）----------------------------------

$expectedPlugins = [ordered]@{
    'PotionCraft_AutoHaggle.dll'      = 'BB2D66D2928621578BEBBA99DB66F1A238DD04D0A788983C721BF6ECE36890EC'
    'PotionCraft_SortBookmark.dll'    = '16D36EDEFB5021EC8E4B98C3DAC48235AE9AC9B6F3C32591CAEC7132DFC18500'
    'PotionCraft_MoreInformation.dll' = '9A3FC4FB2B3124BDAD8C187502DC6697E2622DF0DDEC91C50F41CCD0BFEBBFBE'
    'PotionCraftAutoGarden.dll'       = '31A3FA3167A447DF478DBB3875A5D39E84B7872A4D51A18DEB60DF8BB7CC9ECB'
    'Ukersn''s TweakWizard.dll'       = 'EA961E2F70AF6010382B932DC1EEE148661AAF67259D53CC6675B437CEB4FBED'
    'MagicShelf.dll'                  = '963E213FD8D0B09DBF1FA22862520EC7FEA87DCCA821A134FEF648409A20E302'
}

$expectedFramework = @(
    'winhttp.dll'
    'doorstop_config.ini'
    '.doorstop_version'
    'BepInEx\core\BepInEx.Preloader.dll'
    'BepInEx\core\BepInEx.dll'
)

$expectedConfigs = @(
    'BepInEx\config\com.ukersn.plugin.AutoGarden.cfg'
    'BepInEx\config\com.ukersn.plugin.TweakWizard.cfg'
    'BepInEx\config\me.xiaoye97.plugin.PotionCraft.MoreInformation.cfg'
    'BepInEx\config\me.xiaoye97.plugin.PotionCraft.SortBookmark.cfg'
    'BepInEx\config\gooddayday.potioncraft.magicshelf.cfg'
)

# ---- 自动定位游戏目录 -------------------------------------------------------

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
            $raw = Get-Content $vdf -Raw
            foreach ($m in [regex]::Matches($raw, '"path"\s*"([^"]+)"')) {
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

# ---- 主流程 -----------------------------------------------------------------

if (-not $GamePath) {
    $GamePath = Find-GamePath
    if (-not $GamePath) {
        Write-Host '未能自动定位游戏目录，请用 -GamePath 指定（含 Potion Craft.exe 的那一层）。' -ForegroundColor Red
        exit 2
    }
    Write-Host "已自动定位游戏目录：$GamePath"
}

if (-not (Test-Path $GamePath)) {
    Write-Host "游戏目录不存在：$GamePath" -ForegroundColor Red
    exit 2
}

$failCount = 0
$okCount   = 0

Write-Host ''
Write-Host '=== 1. 框架文件（BepInEx） ===' -ForegroundColor Cyan
foreach ($rel in $expectedFramework) {
    $full = Join-Path $GamePath $rel
    if (Test-Path $full) {
        Write-Host ('  [OK]   ' + $rel) -ForegroundColor Green
        $okCount++
    } else {
        Write-Host ('  [缺失] ' + $rel) -ForegroundColor Red
        $failCount++
    }
}

$doorstop = Join-Path $GamePath 'doorstop_config.ini'
if (Test-Path $doorstop) {
    $line = Select-String -Path $doorstop -Pattern '^\s*enabled\s*=' | Select-Object -First 1
    if ($line) {
        $value = ($line.Line -split '=', 2)[1].Trim()
        if ($value -eq 'true') {
            Write-Host '  [OK]   doorstop_config.ini: enabled = true' -ForegroundColor Green
        } else {
            Write-Host ('  [注意] doorstop_config.ini: enabled = ' + $value + '（应为 true，否则注入不生效）') -ForegroundColor Yellow
            $failCount++
        }
    }
}

Write-Host ''
Write-Host '=== 2. 插件 dll ===' -ForegroundColor Cyan
$pluginDir = Join-Path $GamePath 'BepInEx\plugins'
if (-not (Test-Path $pluginDir)) {
    Write-Host '  [缺失] BepInEx\plugins 目录不存在' -ForegroundColor Red
    $failCount++
} else {
    foreach ($name in $expectedPlugins.Keys) {
        $full = Join-Path $pluginDir $name
        if (-not (Test-Path $full)) {
            Write-Host ('  [缺失] ' + $name) -ForegroundColor Red
            $failCount++
            continue
        }
        $hash = (Get-FileHash -Path $full -Algorithm SHA256).Hash
        if ($hash -eq $expectedPlugins[$name]) {
            Write-Host ('  [OK]   ' + $name) -ForegroundColor Green
            $okCount++
        } else {
            Write-Host ('  [不一致] ' + $name) -ForegroundColor Yellow
            Write-Host ('           期望 ' + $expectedPlugins[$name])
            Write-Host ('           实际 ' + $hash)
            $failCount++
        }
    }

    Write-Host ''
    Write-Host '=== 3. 清单之外的 dll（自己新装的请更新 mods/manifest.md） ===' -ForegroundColor Cyan
    $extras = @(Get-ChildItem -Path $pluginDir -Filter *.dll -File | Where-Object { -not $expectedPlugins.Contains($_.Name) })
    if ($extras.Count -eq 0) {
        Write-Host '  （无）' -ForegroundColor Green
    } else {
        foreach ($e in $extras) {
            Write-Host ('  [额外] ' + $e.Name + '  ' + $e.Length + ' B') -ForegroundColor Yellow
        }
    }
}

Write-Host ''
Write-Host '=== 4. 配置文件（首次运行游戏后才会生成） ===' -ForegroundColor Cyan
foreach ($rel in $expectedConfigs) {
    $full = Join-Path $GamePath $rel
    if (Test-Path $full) {
        Write-Host ('  [OK]   ' + (Split-Path $rel -Leaf)) -ForegroundColor Green
    } else {
        Write-Host ('  [缺失] ' + (Split-Path $rel -Leaf) + '（启动过一次游戏后应存在）') -ForegroundColor Yellow
    }
}

Write-Host ''
if ($failCount -eq 0) {
    Write-Host ('校验通过：' + $okCount + ' 项一致。') -ForegroundColor Green
    exit 0
} else {
    Write-Host ('发现 ' + $failCount + ' 项问题，请对照 docs/troubleshooting.md 处理。') -ForegroundColor Red
    exit 1
}
