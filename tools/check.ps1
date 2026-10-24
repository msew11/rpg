param([switch]$ExportWindows, [switch]$Tests)

$ErrorActionPreference = 'Stop'

$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$candidates = @()
if ($env:GODOT_BIN) {
    if (-not (Test-Path -LiteralPath $env:GODOT_BIN -PathType Leaf)) {
        throw "GODOT_BIN does not point to a file: $env:GODOT_BIN"
    }
    $candidates += $env:GODOT_BIN
}
foreach ($name in @('godot-mono', 'godot', 'godot4')) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
}
$candidates += 'D:\Program Files\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$candidates += 'D:\Program Dev\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'

$godot = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $godot) {
    throw 'Godot .NET executable not found. Set GODOT_BIN to the Godot 4.7.2 .NET console executable path.'
}

$version = (& $godot --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -notmatch '^4\.7\.2\.stable\.mono\.') {
    throw "Godot 4.7.2 .NET required; found: $version"
}
Write-Host "Using $godot ($version)"

Write-Host 'Building C# project...'
& dotnet build (Join-Path $projectPath 'rpg.csproj') --nologo
if ($LASTEXITCODE -ne 0) {
    throw 'C# project build failed.'
}

function Invoke-GodotCheck {
    param([string]$Name, [string[]]$GodotArgs)
    Write-Host "Checking $Name..."
    $output = & $godot @GodotArgs 2>&1
    $exitCode = $LASTEXITCODE
    $output | ForEach-Object { Write-Host $_ }
    if ($exitCode -ne 0 -or ($output | Out-String) -match '(?m)^(?:SCRIPT )?ERROR:') {
        throw "$Name failed (exit code $exitCode)."
    }
}

Invoke-GodotCheck 'project import' @('--headless', '--path', $projectPath, '--editor', '--quit')
Invoke-GodotCheck 'main scene' @('--headless', '--path', $projectPath, '--quit-after', '2')

if ($Tests) {
    Write-Host 'Running .NET tests...'
    & dotnet test (Join-Path $projectPath 'tests\ClientTests\ClientTests.csproj') --nologo
    if ($LASTEXITCODE -ne 0) { throw '.NET tests failed.' }
    $testAssembly = Join-Path $projectPath 'tests\ClientTests\bin\Debug\net8.0\ClientTests.dll'
    Invoke-GodotCheck 'Godot xUnit tests' @('--headless', '--path', $projectPath,
        'res://tests/godot_test_host.tscn', '--', $testAssembly)
    # 外观与角色管理必须通过真实节点、帧推进和按钮验收。
    Invoke-GodotCheck 'actor animation' @('--headless', '--path', $projectPath, 'res://tests/actor_animation_smoke.tscn')
    Invoke-GodotCheck 'role management' @('--headless', '--path', $projectPath, 'res://tests/role_management_smoke.tscn')
    # 图鉴验收覆盖全部素材目录及实际 GM 分类浏览交互。
    Invoke-GodotCheck 'item catalog' @('--headless', '--path', $projectPath, 'res://tests/item_catalog_smoke.tscn')
    # 指针与点地反馈验收覆盖完整图块、真实输入隔离及淡出释放。
    Invoke-GodotCheck 'pointer feedback' @('--headless', '--path', $projectPath, 'res://tests/pointer_feedback_smoke.tscn')
    # 背包与 GM 通过真实聊天、图标格子和重启存档验收。
    Invoke-GodotCheck 'bag and GM' @('--headless', '--path', $projectPath, 'res://tests/bag_gm_smoke.tscn')
    # 物品移动必须经过真实鼠标点击，而非直接调用控件回调。
    Invoke-GodotCheck 'item movement' @('--headless', '--path', $projectPath, 'res://tests/item_movement_smoke.tscn')
    # 普通弹窗与 HUD 在缩放、加载和拖动后都不得越出窗口。
    Invoke-GodotCheck 'UI bounds' @('--headless', '--path', $projectPath, 'res://tests/ui_bounds_smoke.tscn')
}

if ($ExportWindows) {
    $templateDir = Join-Path $env:APPDATA 'Godot\export_templates\4.7.2.stable.mono'
    $requiredTemplates = @('windows_debug_x86_64.exe', 'windows_release_x86_64.exe')
    $missingTemplates = @($requiredTemplates | Where-Object { -not (Test-Path -LiteralPath (Join-Path $templateDir $_) -PathType Leaf) })
    if ($missingTemplates.Count -gt 0) {
        $missingPaths = ($missingTemplates | ForEach-Object { Join-Path $templateDir $_ }) -join ', '
        throw "Godot 4.7.2 .NET Windows export templates are missing: $missingPaths. Install the matching Mono Export Templates via Editor > Manage Export Templates, then retry."
    }
    $exportPath = Join-Path $projectPath 'build\windows\rpg.exe'
    New-Item -ItemType Directory -Force -Path (Split-Path $exportPath) | Out-Null
    Invoke-GodotCheck 'Windows export' @('--headless', '--path', $projectPath, '--export-release', 'Windows Desktop', $exportPath)
    if (-not (Test-Path -LiteralPath $exportPath -PathType Leaf)) {
        throw "Windows export did not produce $exportPath"
    }
    Write-Host "Exported $exportPath"
    $game = Start-Process -FilePath $exportPath -ArgumentList '--quit-after 10' -PassThru -Wait -WindowStyle Hidden
    if ($game.ExitCode -ne 0) {
        throw "Exported game failed to start (exit code $($game.ExitCode))."
    }
    Write-Host 'Exported game launch passed.'

    $exportDir = Split-Path $exportPath
    $archivePath = Join-Path $projectPath 'build\rpg-windows.zip'
    $temporaryArchive = Join-Path $projectPath ("build\rpg-windows-$([guid]::NewGuid().ToString('N')).zip")
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    try {
        [System.IO.Compression.ZipFile]::CreateFromDirectory($exportDir, $temporaryArchive, [System.IO.Compression.CompressionLevel]::Optimal, $false)
        $archive = [System.IO.Compression.ZipFile]::OpenRead($temporaryArchive)
        try {
            $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
            if ('rpg.exe' -notin $entries -or 'data_rpg_windows_x86_64/coreclr.dll' -notin $entries) {
                throw 'Windows archive is missing the executable or .NET runtime files.'
            }
        }
        finally {
            $archive.Dispose()
        }
        Move-Item -LiteralPath $temporaryArchive -Destination $archivePath -Force
        Write-Host "Packaged $archivePath"
    }
    finally {
        if (Test-Path -LiteralPath $temporaryArchive -PathType Leaf) {
            Remove-Item -LiteralPath $temporaryArchive
        }
    }
}
Write-Host 'All checks passed.'
