## MODIFIED Requirements

### Requirement: ECS 单例仓库
`LocalDataRepoCache` SHALL 在初始化时创建唯一的 `DataRepoEcsWorld : DataRepoSingle<DataEcsWorld>` 仓库及其数据，并按仓库精确 C# 类型返回该实例。`WorldState` 构造时 SHALL 接收缓存并把该仓库传给 `EcsWorld`，使世界使用仓库已有的同一个 `DataEcsWorld`；GDScript SHALL 能通过缓存提供的创建方法构造世界。缓存 SHALL 不提供后续绑定世界数据的方法。

#### Scenario: 初始化时可取得仓库
- **WHEN** 新建 `LocalDataRepoCache`，尚未创建 `WorldState`
- **THEN** 已可取得唯一的 ECS 仓库及其数据实例

#### Scenario: 从仓库数据创建世界
- **WHEN** 会话通过缓存创建 `WorldState` 后创建或更新实体
- **THEN** 仓库承载的数据实例与 ECS 正在操作的实例相同
