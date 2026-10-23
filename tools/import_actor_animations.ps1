# 根据 art_export/基础角色_男女全部动作 的解码清单重建身体动画，不修改来源文件。
param([Parameter(Mandatory = $true)][string]$SourcePath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceRoot = (Resolve-Path -LiteralPath $SourcePath).Path
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$animations = Get-Content -LiteralPath (Join-Path $sourceRoot 'animation_frames.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($animations.Count -ne 26) { throw '应提供男女各 13 个动作。' }
$knownActions = @('stand', 'run', 'sit', 'die', 'attack1', 'attack2', 'attack3', 'magic', 'prepare1', 'prepare2', 'prepare3', 'hurt', 'ride')
$seen = @{}
$total = 0
foreach ($animation in $animations) {
    $gender = switch ($animation.gender) { '男性' { 'male' } '女性' { 'female' } default { throw '未知性别。' } }
    $actionName = [string]$animation.name
    if ($actionName -notin $knownActions -or $seen.ContainsKey("$gender/$actionName")) { throw '动作未知或重复。' }
    $seen["$gender/$actionName"] = $true
    $count = [int]$animation.framesPerDirection
    if ($animation.directions -ne 5 -or $count -lt 1 -or $animation.frames.Count -ne 5 * $count) { throw '方向或帧数不完整。' }
    $width = [int]$animation.width
    $height = [int]$animation.height
    $target = Join-Path $projectRoot "assets/characters/$gender"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    $atlas = New-Object System.Drawing.Bitmap ($width * $count), ($height * 5)
    $graphics = [System.Drawing.Graphics]::FromImage($atlas)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('[gd_resource type="Resource" script_class="DataActorAnimation" load_steps=' + (5 * $count + 4) + ' format=3]')
    $lines.Add('')
    $lines.Add('[ext_resource type="Script" path="res://src/World/Views/DataActorAnimation.cs" id="1"]')
    $lines.Add('[ext_resource type="Texture2D" path="res://assets/characters/' + "$gender/$actionName.png" + '" id="2"]')
    $frameKeys = @{}
    try {
        foreach ($frame in $animation.frames) {
            $direction = [int]$frame.direction
            $index = [int]$frame.frame
            $key = "${direction}_$index"
            if ($direction -lt 0 -or $direction -ge 5 -or $index -lt 0 -or $index -ge $count -or $frameKeys.ContainsKey($key)) { throw '帧索引越界或重复。' }
            $frameKeys[$key] = $true
            $framePath = [System.IO.Path]::GetFullPath((Join-Path $sourceRoot $frame.path))
            if (-not $framePath.StartsWith($sourceRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw '帧路径超出来源目录。' }
            $image = [System.Drawing.Image]::FromFile($framePath)
            try {
                if ($image.Width -ne $width -or $image.Height -ne $height) { throw "帧尺寸不一致：$framePath" }
                $graphics.DrawImageUnscaled($image, $index * $width, $direction * $height)
            }
            finally { $image.Dispose() }
            $lines.Add('')
            $lines.Add('[sub_resource type="AtlasTexture" id="Frame_' + $key + '"]')
            $lines.Add('atlas = ExtResource("2")')
            $lines.Add("region = Rect2($($index * $width), $($direction * $height), $width, $height)")
            $lines.Add('filter_clip = true')
        }
        $atlas.Save((Join-Path $target "$actionName.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $atlas.Dispose() }
    $lines.Add('')
    $lines.Add('[sub_resource type="SpriteFrames" id="Animations"]')
    $lines.Add('animations = [')
    $loop = if ($actionName -in @('stand', 'run')) { 'true' } else { 'false' }
    for ($direction = 0; $direction -lt 5; $direction++) {
        $lines.Add('{"frames": [')
        for ($index = 0; $index -lt $count; $index++) {
            $suffix = if ($index -lt $count - 1) { ',' } else { '' }
            $lines.Add('{"duration": 1.0, "texture": SubResource("Frame_' + "${direction}_$index" + '")}' + $suffix)
        }
        $suffix = if ($direction -lt 4) { ',' } else { '' }
        $lines.Add('], "loop": ' + $loop + ', "name": &"dir' + $direction + '", "speed": 10.0}' + $suffix)
    }
    $lines.Add(']')
    $lines.Add('')
    $lines.Add('[resource]')
    $lines.Add('script = ExtResource("1")')
    $lines.Add('Frames = SubResource("Animations")')
    $lines.Add("Anchor = Vector2($($animation.anchorX), $($animation.anchorY))")
    [System.IO.File]::WriteAllLines((Join-Path $target "$actionName.tres"), $lines, [System.Text.UTF8Encoding]::new($false))
    $total += $animation.frames.Count
}
if ($total -ne 605) { throw "总帧数与资源约定不一致：$total" }
Write-Host "已导入 $($animations.Count) 个动作，$total 帧。"
