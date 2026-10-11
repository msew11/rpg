## Why

核心已将世界数据访问抽象为 `IEcsWorldData`，开发层需要提供具体实现并保持本地会话的共享数据行为。

## What Changes

- 将世界数据实现放在 `scripts/data/EcsWorldData.cs`，仓库改名为 `EcsWorldDataRepo`。
- 更新本地缓存、世界创建、测试和开发文档中的引用。

## Capabilities

### Modified Capabilities

- `session-data-repository`: 会话仓库提供新命名的数据实例并交给 ECS 世界。

## Impact

具体类名和仓库类型变更；实体与组件行为保持一致。
