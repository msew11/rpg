# session-data-repository Specification

## Purpose

使本地会话独占统一数据缓存，由单例仓库承载其 ECS 世界数据。

## Requirements

### Requirement: 会话独占数据缓存
每个 `LocalSession` SHALL 从 GDScript 创建并持有独立的 `LocalDataRepoCache`，该缓存 SHALL 不依赖场景树。不同会话的 ECS 数据实例 SHALL 相互独立。

#### Scenario: 多个本地会话
- **WHEN** 创建两个 `LocalSession`
- **THEN** 它们持有不同缓存和不同 ECS 世界数据实例

### Requirement: ECS 单例仓库
`LocalDataRepoCache` SHALL 按 `RepoWorldEcs` 的精确 C# 类型返回一个 `DataRepoSingle<DataEcsWorld>` 仓库。仓库与 `EcsWorld` SHALL 引用同一个 `DataEcsWorld`；同一世界 SHALL 只绑定一个缓存。

#### Scenario: 世界数据绑定
- **WHEN** 会话将世界绑定至其缓存后创建或更新实体
- **THEN** 仓库承载的数据实例与 ECS 正在操作的实例相同

#### Scenario: 重复绑定
- **WHEN** 已绑定缓存的世界再次绑定缓存
- **THEN** 操作失败且原绑定保持不变
