# Design

## Context

项目已有 Godot 4.7.2 的 `Windows Desktop` 预设及 `tools/check.ps1 -ExportWindows`，脚本会先运行检查，导出到 `build/windows/rpg.exe`，再启动导出程序验证。当前预设未启用 PCK 嵌入；`.idea/` 被忽略，项目尚无共享运行配置。

## Goals / Non-Goals

**Goals:**
- Rider 下通过一个可共享的运行配置重复执行检查、单文件导出和启动验证。
- 命令行的同一导出入口保持可用。

**Non-Goals:**
- 更改游戏玩法或发布流水线。

## Decisions

- 在 `export_presets.cfg` 设置 `binary_format/embed_pck=true`，让所有 Windows Desktop 导出采用同一单文件格式，避免运行时临时改写预设。
- 将配置保存在可提交的 `.run/*.run.xml`，使用 Rider 内置 Shell Script 支持执行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -ExportWindows`，工作目录为项目根目录。脚本继续负责 Godot 路径查找、版本检查和启动验证。
- README 给出配置名称、操作方法与产物路径。

## Risks / Trade-offs

- Rider 需要启用其 Shell Script 插件；该插件在本机 Rider 安装中存在。若未启用，可使用 README 中相同的 PowerShell 命令。
- 输出目录可能保留旧的 `.pck`；验收以新导出的 `.exe` 可脱离 `.pck` 启动为准。
