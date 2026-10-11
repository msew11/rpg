## Why

Windows 导出缺少 Godot .NET 模板时，Windows PowerShell 5.1 可能只显示 `check.ps1` 的调用行，掩盖实际缺失路径。

## What Changes

- 在调用 Godot 导出前检查所需 Windows Mono 模板，并给出具体缺失路径和安装提示。

## Capabilities

### New Capabilities

- `windows-export-check`: Windows 导出检查失败时提供可操作的诊断信息。

### Modified Capabilities

无。

## Impact

仅影响 `tools/check.ps1` 的导出前置检查；不改变游戏运行与导出预设。
