## Why

世界的创建入口应属于 `WorldState`，缓存只负责创建和持有仓库。

## What Changes

- 将 GDScript 可调用的世界创建方法从 `LocalDataRepoCache` 移到 `WorldState`。
- 保留 C# 的 `WorldState(LocalDataRepoCache)` 构造函数，更新 GDScript 调用方。

## Capabilities

### Modified Capabilities
- `session-data-repository`: 世界创建入口位于 `WorldState`。

## Impact

修改会话、世界、功能测试与数据层文档；Cache 的仓库初始化行为不变。
