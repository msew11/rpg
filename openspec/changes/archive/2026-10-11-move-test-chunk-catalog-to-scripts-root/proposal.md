## Why

`TestChunkCatalog` 是本地会话启动时读取测试内容的入口，不属于 `scripts/world/` 中的 ECS 世界状态与规则。将其放在脚本根目录，使文件位置与既定职责一致。

## What Changes

- 将 `test_chunk_catalog.gd` 及其 Godot UID 从 `scripts/world/` 移到 `scripts/`，保持类名与读取行为不变。
- 更新脚本目录规格和架构说明。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-script-boundaries`: 明确测试内容读取器与入口脚本同在 `scripts/` 根目录。

## Impact

仅涉及脚本路径、目录规格与架构文档；`scripts/core/`、测试地图数据、玩法和画面行为不变。
