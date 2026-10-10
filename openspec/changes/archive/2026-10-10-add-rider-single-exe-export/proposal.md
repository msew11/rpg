# Proposal

## Why

Windows 单文件导出已能通过临时调整 Godot 预设完成，但每次都需要手动操作。希望在 Rider 中直接选择运行配置完成相同的导出与启动验证。

## What Changes

- 让现有 Windows Desktop 导出预设默认嵌入 PCK，生成可独立运行的 exe。
- 提交一个共享的 Rider Shell Script 运行配置，调用现有检查与导出脚本。
- 在 README 中说明 Rider 操作和输出位置。

## Capabilities

此项只调整本地开发工具链，不改变游戏运行行为；不新增或修改游戏能力规范。

## Impact

`export_presets.cfg`、`.run/`、`README.md` 和 Windows 导出产物；不增加运行时依赖。
