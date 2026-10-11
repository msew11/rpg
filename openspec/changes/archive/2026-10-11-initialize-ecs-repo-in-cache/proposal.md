## Why

ECS 仓库应从缓存创建时起固定存在，世界只能使用仓库先创建的数据，避免在世界创建后再把数据绑定到缓存。

## What Changes

- `LocalDataRepoCache` 初始化时创建 ECS 单例仓库及其数据，删除 `BindWorld`。
- `WorldState` 构造时接收 Cache，再把仓库数据传给 `EcsWorld`。
- 将仓库更名为 `DataRepoEcsWorld`，更新 GDScript 创建路径及测试。

## Capabilities

### Modified Capabilities
- `session-data-repository`: 明确仓库先于世界创建且始终可访问。
- `data-repository`: ECS 仓库采用用户指定的专名。

## Impact

修改 `develop` 的数据、世界、会话、测试与文档；不修改核心层或存档行为。
