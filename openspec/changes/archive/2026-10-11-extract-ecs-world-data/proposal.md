## Why

世界实体与组件需要作为统一数据仓库管理的单个数据对象，同时保留 ECS 的现有操作入口与系统调度行为。

## What Changes

- 将实体标识分配状态、实体集合和组件集合迁入 `DataEcsWorld`。
- 允许 `EcsWorld` 使用外部提供的同一数据实例，默认构造仍创建独立数据。
- 系统列表和调度令牌继续由 `EcsWorld` 管理。

## Capabilities

### Modified Capabilities
- `ecs-runtime`: 实体数据可由多个操作入口共享。
- `data-repository`: 核心提供一个 ECS 状态数据类型，供具体仓库承载。

## Impact

修改核心 ECS 与数据层，补充核心回归测试和文档，不实现具体仓库或存档格式。
