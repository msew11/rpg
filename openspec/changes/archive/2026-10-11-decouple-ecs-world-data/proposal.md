## Why

核心 ECS 当前直接依赖具体的 `DataEcsWorld` 和仓库类型，阻碍世界数据由开发层实现。

## What Changes

- 在核心 ECS 中定义 `IEcsWorldData` 状态访问契约。
- `EcsWorld` 只接收该契约，不再创建具体数据或接收仓库。
- 删除核心的具体世界数据类；核心测试使用测试实现验证共享数据行为。

## Capabilities

### Modified Capabilities

- `ecs-runtime`: 世界通过状态契约访问实体与组件。
- `data-repository`: 核心只提供仓库抽象，不提供 ECS 具体数据。

## Impact

调用方须传入实现 `IEcsWorldData` 的对象；开发分支将提供具体实现并迁移调用方。
