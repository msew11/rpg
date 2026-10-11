## ADDED Requirements

### Requirement: 实体数据独立承载
运行时 SHALL 将下一个实体标识、有效实体集合和组件索引保存在实现 `IDataRepoData` 的 `DataEcsWorld` 中。`EcsWorld` SHALL 能使用指定数据实例；不指定时 SHALL 创建独立实例。系统注册和调度状态 SHALL 留在 `EcsWorld` 中。

#### Scenario: 共享实体数据
- **WHEN** 两个 `EcsWorld` 先后使用同一个 `DataEcsWorld`
- **THEN** 后一个世界能够读取已有实体及组件，并继续分配不重复的实体标识
