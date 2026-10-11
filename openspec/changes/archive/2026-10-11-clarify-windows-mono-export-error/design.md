## Context

`tools/check.ps1` 将 `$ErrorActionPreference` 设置为 `Stop`，Godot 的配置错误写向 stderr。Windows PowerShell 5.1 在 `2>&1` 收集阶段可能抛出 `NativeCommandError`，导致原有输出检查无法给出明确恢复步骤。

## Goals / Non-Goals

- 目标：在导出前识别缺少模板，并给出具体恢复步骤。
- 不在仓库中分发体积约 1.2 GB 的导出模板。

## Decisions

- 仅在 `-ExportWindows` 路径检查 `%APPDATA%/Godot/export_templates/4.7.2.stable.mono/` 中的 Windows x86_64 debug 和 release 模板。
- 缺失时在调用 Godot 导出前终止，并输出缺失路径和安装 Mono 模板的提示。

## Risks / Trade-offs

- 本检查针对当前固定的 Godot 4.7.2 .NET 版本；升级编辑器时需要同步更新模板路径。
