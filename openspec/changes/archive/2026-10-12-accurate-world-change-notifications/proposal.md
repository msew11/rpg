# Proposal

## Why

世界事件携带可由调用方写入的变化标记，通知依赖处理 System 主动设标记。调用方可伪造刷新，处理器漏标又会使视图错过真实变化。

## What Changes

- 由世界边界根据处理前后的可观察状态发出玩家、区块和调试通知。
- 移除事件上的公开变化标记；无效或重复命令不产生伪通知。
- 保持 `send_event()` 当前的“是否有处理者”返回语义，结果语义留给路线图第 5 项。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-interaction-events`：补充世界交互变化通知的准确性与覆盖场景。

## Impact

影响 `scripts/world/WorldManager.cs`、世界事件和处理 System、世界通知测试及架构、路线图文档。无需变更存档格式或 UI 调用方式。
