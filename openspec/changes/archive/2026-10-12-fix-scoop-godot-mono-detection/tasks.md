# Tasks

## 1. 编辑器发现

- [x] 1.1 在 PATH 候选中优先查找 `godot-mono`，以 Scoop shim 的 `--version` 输出验证可发现 4.7.2 .NET。

## 2. 集成验收

- [x] 2.1 运行 `openspec validate fix-scoop-godot-mono-detection --strict`、`tools/check.ps1` 及 `tools/check.ps1 -ExportWindows`，记录检查和导出结果。
