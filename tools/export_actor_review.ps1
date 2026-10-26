# 从正式ActorView导出完整实绘，再用Pillow整理为可离线审查的图片和ZIP。
param([string]$OutputDirectory, [string]$Python = 'python')

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $OutputDirectory) {
    $reviewTime = [TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow, 'China Standard Time')
    $OutputDirectory = Join-Path $projectPath ("build\actor-review-" + $reviewTime.ToString('yyyy-MM-dd-HHmmss'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Output directory already exists: $OutputDirectory" }
& $Python -c 'from PIL import Image'
if ($LASTEXITCODE -ne 0) { throw 'Python with Pillow is required. Specify -Python with an appropriate interpreter.' }
$godot = if ($env:GODOT_BIN) { $env:GODOT_BIN } else { (Get-Command godot-mono -ErrorAction Stop).Source }
$version = (& $godot --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $version -notmatch '^4\.7\.2\.stable\.mono\.') { throw "Godot 4.7.2 .NET required: $version" }
Push-Location $projectPath
try {
    $revision = (git rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source revision.' }
    & dotnet build --nologo
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    # 使用隐藏窗口运行GPU渲染；日志放于导出目录旁，失败时保留诊断。
    $renderLog = "$OutputDirectory.render.log"
    $errorLog = "$OutputDirectory.render-error.log"
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($OutputDirectory)) | Out-Null
    $arguments = @('--path', ('"' + $projectPath + '"'), 'res://tests/actor_review_export.tscn', '--',
        ('"--output=' + $OutputDirectory + '"'), ('--revision=' + $revision))
    $process = Start-Process -FilePath $godot -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait `
        -RedirectStandardOutput $renderLog -RedirectStandardError $errorLog
    Get-Content -LiteralPath $renderLog
    if ($process.ExitCode -ne 0 -or (Get-Content -LiteralPath $errorLog -Raw) -match '(?m)^ERROR:') {
        Get-Content -LiteralPath $errorLog
        throw "Actor rendering failed. See $errorLog"
    }
    & $Python -B (Join-Path $PSScriptRoot 'build_actor_review.py') $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Review packaging or coverage validation failed.' }
    Write-Host "Review: $OutputDirectory\index.html"
    Write-Host "Archive: $OutputDirectory.zip"
}
finally {
    Pop-Location
}
