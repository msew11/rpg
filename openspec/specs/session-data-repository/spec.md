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
`LocalDataRepoCache` SHALL 在初始化时创建唯一的 `DataRepoEcsWorld : DataRepoSingle<DataEcsWorld>` 仓库及其数据，并按仓库精确 C# 类型返回该实例。`WorldState` 构造时 SHALL 接收缓存并把该仓库传给 `EcsWorld`，使世界使用仓库已有的同一个 `DataEcsWorld`；GDScript SHALL 能通过 `WorldState` 自身的静态创建方法调用该构造函数。缓存 SHALL 不提供世界创建或后续绑定世界数据的方法。

#### Scenario: 初始化时可取得仓库
- **WHEN** 新建 `LocalDataRepoCache`，尚未创建 `WorldState`
- **THEN** 已可取得唯一的 ECS 仓库及其数据实例

#### Scenario: 从仓库数据创建世界
- **WHEN** 会话通过 `WorldState.Create(cache)` 创建世界后创建或更新实体
- **THEN** 仓库承载的数据实例与 ECS 正在操作的实例相同
