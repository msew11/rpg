param([switch]$ExportWindows)

$ErrorActionPreference = 'Stop'

$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$candidates = @()
if ($env:GODOT_BIN) {
    if (-not (Test-Path -LiteralPath $env:GODOT_BIN -PathType Leaf)) {
        throw "GODOT_BIN does not point to a file: $env:GODOT_BIN"
    }
    $candidates += $env:GODOT_BIN
}
foreach ($name in @('godot', 'godot4')) {
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
Invoke-GodotCheck 'core C# tests' @('--headless', '--path', $projectPath, '--script', 'res://tests/core/TestEcs.cs')
Invoke-GodotCheck 'world ECS state' @('--headless', '--path', $projectPath, '--script', 'res://tests/TestWorldEcs.cs')
Invoke-GodotCheck 'movement behavior' @('--headless', '--path', $projectPath, '--script', 'tests/test_movement.gd')
Invoke-GodotCheck 'view rebuilding' @('--headless', '--path', $projectPath, '--script', 'tests/test_view.gd')
Invoke-GodotCheck 'chunk coordinates' @('--headless', '--path', $projectPath, '--script', 'res://tests/test_chunk_coordinates.gd')
Invoke-GodotCheck 'manual chunk cfg' @('--headless', '--path', $projectPath, '--script', 'res://tests/test_manual_chunk_cfg.gd')
Invoke-GodotCheck 'world state data' @('--headless', '--path', $projectPath, '--script', 'res://tests/test_world_state_data.gd')
Invoke-GodotCheck 'chunk world state' @('--headless', '--path', $projectPath, '--script', 'res://tests/test_chunk_world.gd')
Invoke-GodotCheck 'chunk display' @('--headless', '--path', $projectPath, '--script', 'res://tests/test_chunk_view.gd')

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
}
Write-Host 'All checks passed.'
