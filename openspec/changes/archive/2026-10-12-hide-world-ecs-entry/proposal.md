# Proposal

## Why

`WorldManager.ecs` 让世界入口的调用方直接增删实体、组件和 System，可能绕过区块索引与通知。本次先封闭该入口，为后续各项边界收口建立稳定起点。

## What Changes

- **BREAKING** 移除 `WorldManager` 公开的裸 `EcsWorld` 属性，由管理器内部独占 ECS 调度。
- 将既有测试的 ECS 检查与样本布置移到 `tests/` 中的辅助类，保留实体关系和持久化验收。
- 更新世界边界文档与路线图状态。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-script-boundaries`: 世界管理器不向生产调用方暴露可直接修改的 ECS 世界。

## Impact

影响 `scripts/world/WorldManager.cs` 的公开 API、依赖该 API 的测试以及世界边界文档；不改变游戏存档格式或玩法。
