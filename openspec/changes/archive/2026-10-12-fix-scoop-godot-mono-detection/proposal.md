# Proposal

## Why

Scoop 安装的 Godot 4.7.2 .NET 提供 `godot-mono` 命令，但检查脚本只查找 `godot` 和 `godot4`，导致 Windows 导出在实际已安装编辑器时提前失败。

## What Changes

- 检查脚本在 PATH 中识别 `godot-mono`，并优先于通用 Godot 命令使用它。
- 保留 `GODOT_BIN` 覆盖及 4.7.2 .NET 版本校验。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `windows-export-check`: 在 PATH 中有 Scoop 的 Godot .NET 命令时，Windows 导出检查能找到并使用该编辑器。

## Impact

影响 `tools/check.ps1` 的编辑器查找及 Windows 导出检查；不改变游戏代码和导出预设。
