## MODIFIED Requirements

### Requirement: 实体数据独立承载

运行时 SHALL 通过核心 `IEcsWorldData` 接口读取和修改下一个实体标识、有效实体集合和组件索引。`EcsWorld` SHALL 接收由调用方提供的非空实现，并允许多个世界先后使用同一实例；核心 SHALL 不创建具体世界数据或依赖具体仓库。系统注册和调度状态 SHALL 留在 `EcsWorld` 中。

#### Scenario: 共享实体数据

- **WHEN** 两个 `EcsWorld` 先后使用同一个 `IEcsWorldData` 实例
- **THEN** 后一个世界能够读取已有实体及组件，并继续分配不重复的实体标识

#### Scenario: 缺失数据实例

- **WHEN** 调用方构造世界时传入空数据实例
- **THEN** 构造过程报告参数错误
