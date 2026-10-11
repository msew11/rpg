# windows-export-check Specification

## Purpose
定义 Windows .NET 导出检查在缺少匹配模板时应给出的诊断。

## Requirements

### Requirement: Windows 导出检查报告模板配置错误

检查脚本 SHALL 在调用 Godot Windows 导出前检查与当前 Godot .NET 编辑器版本匹配的 Windows Mono 模板。若模板缺失，脚本 SHALL 列出缺失路径并提示安装匹配的 Mono Export Templates。

#### Scenario: 缺少 Windows Mono 导出模板

- **WHEN** 开发者运行 `tools/check.ps1 -ExportWindows`，本机缺少对应导出模板
- **THEN** 检查在导出前失败，并显示缺失路径及安装提示
