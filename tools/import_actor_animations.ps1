# 从只读导出素材重建独立身体、逐帧挂接点及全部头部资源。
param([Parameter(Mandatory = $true)][string]$SourcePath, [Parameter(Mandatory = $true)][string]$RawPath)
$ErrorActionPreference = 'Stop'
& python (Join-Path $PSScriptRoot 'import_actor_layers.py') --source $SourcePath --raw $RawPath
if ($LASTEXITCODE -ne 0) { throw '角色分层资源导入失败。' }
& python (Join-Path $PSScriptRoot 'import_actor_weapon.py') --raw $RawPath
if ($LASTEXITCODE -ne 0) { throw '默认长剑资源导入失败。' }
