# 先检查正式启动，再分别执行普通 .NET 与依赖 Godot 的全部现有用例。
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'check.ps1') -Tests
