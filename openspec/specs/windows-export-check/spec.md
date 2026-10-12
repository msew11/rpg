# windows-export-check Specification

## Purpose
定义 Windows .NET 导出检查在缺少匹配模板时应给出的诊断。

## Requirements

### Requirement: Windows 导出检查报告模板配置错误

检查脚本 SHALL 在调用 Godot Windows 导出前检查与当前 Godot .NET 编辑器版本匹配的 Windows Mono 模板。若模板缺失，脚本 SHALL 列出缺失路径并提示安装匹配的 Mono Export Templates。

#### Scenario: 缺少 Windows Mono 导出模板

- **WHEN** 开发者运行 `tools/check.ps1 -ExportWindows`，本机缺少对应导出模板
- **THEN** 检查在导出前失败，并显示缺失路径及安装提示

### Requirement: Windows 导出检查识别 PATH 中的 Godot .NET

检查脚本 SHALL 在 `GODOT_BIN` 未设置时识别 PATH 中的 `godot-mono` 命令，并在通用 Godot 命令之前优先使用它；选定的编辑器仍须通过项目要求的 Godot 4.7.2 .NET 版本校验。

#### Scenario: Scoop 安装的 Godot .NET

- **WHEN** `GODOT_BIN` 未设置、Scoop 的 `godot-mono` 命令在 PATH 中，开发者运行 `tools/check.ps1 -ExportWindows`
- **THEN** 检查使用该 Godot .NET 命令继续执行项目检查和 Windows 导出流程

#### Scenario: 显式指定编辑器

- **WHEN** `GODOT_BIN` 指向有效的 Godot 4.7.2 .NET 可执行文件，且 PATH 中也有 `godot-mono`
- **THEN** 检查优先使用 `GODOT_BIN` 指定的可执行文件
