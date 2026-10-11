## Why

世界状态应从缓存创建的单例仓库取得数据，构造 `EcsWorld` 时直接传入仓库，明确数据所有者。

## What Changes

- `DataRepoSingle<T>` 要求具体仓库提供唯一 `Data` 实例。
- `EcsWorld` 增加接收 `DataRepoSingle<DataEcsWorld>` 的构造入口。

## Capabilities

### Modified Capabilities
- `data-repository`: 单例仓库公开唯一数据实例。
- `ecs-runtime`: 世界可从单例仓库构造。

## Impact

修改 `scripts/core/`、核心测试与核心文档；不修改具体仓库或世界功能代码。
