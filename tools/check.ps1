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
$candidates += 'D:\Program Files\Godot_v4.7.2-stable_win64.exe\Godot_v4.7.2-stable_win64_console.exe'

$godot = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not $godot) {
    throw 'Godot executable not found. Set GODOT_BIN to the Godot 4.7.2 executable path.'
}

$version = (& $godot --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -notmatch '^4\.7\.2\.') {
    throw "Godot 4.7.2 required; found: $version"
}
Write-Host "Using $godot ($version)"

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

if ($ExportWindows) {
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
