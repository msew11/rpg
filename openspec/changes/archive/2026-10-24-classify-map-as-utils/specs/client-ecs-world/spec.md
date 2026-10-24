## MODIFIED Requirements

### Requirement: 地图组件与行为分离

ECS Component SHALL 仅承载数据，不持有服务、管理器或业务方法。DataMapComponent SHALL 保存只读地图尺寸、瓦片大小、默认地块和稀疏地块索引；MapUtils SHALL 承担数据构建及地图计算。初始化世界的 Action SHALL 只携带地图数据，WorldManager SHALL 从世界实体的组件读取数据执行地图计算。

#### Scenario: 地图初始化数据隔离

- **WHEN** 从配置构造地图数据并通过 Action 初始化世界
- **THEN** 组件不包含 MapManager 或 ConfigService，后续原始覆盖格集合变化不改变已构造的地图数据

#### Scenario: 越界输入与权威修正

- **WHEN** 移动 Action 或被接纳的权威修正位置超出地图边界
- **THEN** WorldManager 使用 MapUtils 和世界地图组件将其裁剪至有效逻辑范围，输入发送前采用同一规则
