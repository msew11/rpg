# Spec Delta

## ADDED Requirements

### Requirement: 动态地图对象具有 ECS 身份

当前 AOI 中每个独立动态地图对象 SHALL 对应一个 ECS entity。相同服务器对象 ID 的更新 SHALL 保持本地 entity 身份；离开 AOI、切角或退出世界 SHALL 移除该 entity。服务器快照仍为权威来源，世界表现和拾取 SHALL 读取已同步的 ECS 状态。

#### Scenario: 新增更新与离开
- **WHEN** AOI 依次新增、更新、移除一个地面物品
- **THEN** 对应 entity 依次创建、保留身份并更新组件、销毁

#### Scenario: 重同步与怪物对象
- **WHEN** AOI 重同步包含地面物品和怪物对象
- **THEN** 当前动态 entity 集合与快照一致，每个对象只有一个 entity
