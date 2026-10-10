# Proposal

## Why

`develop` 的玩家模拟仍使用 `WorldState` 自建实体表和静态移动更新，与已合入的 `main` ECS 运行时重复。需要让玩家模拟实际使用共享 ECS，同时保持现有原型行为。

## What Changes

- 玩家位置、意图、速度和朝向改为 `EcsComponent`，实体由 `EcsWorld` 创建，移动由注册的 `EcsSystem` 执行。
- `WorldState` 保留 `DataSource` 边界，把现有玩家键映射至组件，区块与调试数据仍由其内存键值区管理。
- 保留既有输入、八方向轨迹、方向标记、区块、雷达和地图行为，并验证组件与视图读取一致。

## Capabilities

### Modified Capabilities

- `player-movement`: 玩家移动通过共享 ECS 更新，展示层仍能读取一致的位置与朝向。

## Impact

修改 `scripts/simulation/`、相关测试和 `develop` 架构文档；不修改 `scripts/core/`、场景或玩法规则。
