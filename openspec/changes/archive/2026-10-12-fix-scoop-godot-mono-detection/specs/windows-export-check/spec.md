# Spec Delta

## ADDED Requirements

### Requirement: Windows 导出检查识别 PATH 中的 Godot .NET

检查脚本 SHALL 在 `GODOT_BIN` 未设置时识别 PATH 中的 `godot-mono` 命令，并在通用 Godot 命令之前优先使用它；选定的编辑器仍须通过项目要求的 Godot 4.7.2 .NET 版本校验。

#### Scenario: Scoop 安装的 Godot .NET

- **WHEN** `GODOT_BIN` 未设置、Scoop 的 `godot-mono` 命令在 PATH 中，开发者运行 `tools/check.ps1 -ExportWindows`
- **THEN** 检查使用该 Godot .NET 命令继续执行项目检查和 Windows 导出流程

#### Scenario: 显式指定编辑器

- **WHEN** `GODOT_BIN` 指向有效的 Godot 4.7.2 .NET 可执行文件，且 PATH 中也有 `godot-mono`
- **THEN** 检查优先使用 `GODOT_BIN` 指定的可执行文件
