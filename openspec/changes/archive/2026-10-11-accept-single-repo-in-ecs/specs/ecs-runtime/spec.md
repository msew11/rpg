## MODIFIED Requirements

### Requirement: 实体数据独立承载
运行时 SHALL 将下一个实体标识、有效实体集合和组件索引保存在实现 `IDataRepoData` 的 `DataEcsWorld` 中。`EcsWorld` SHALL 能使用指定数据实例或承载该数据的 `DataRepoSingle<DataEcsWorld>`；不指定时 SHALL 创建独立实例。系统注册和调度状态 SHALL 留在 `EcsWorld` 中。

#### Scenario: 共享实体数据
- **WHEN** 两个 `EcsWorld` 先后使用同一个 `DataEcsWorld`
- **THEN** 后一个世界能够读取已有实体及组件，并继续分配不重复的实体标识

#### Scenario: 从单例仓库创建世界
- **WHEN** 使用承载 `DataEcsWorld` 的单例仓库构造 `EcsWorld`
- **THEN** 世界操作的实体数据就是该仓库的唯一数据实例
