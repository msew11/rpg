## Why

DataRepoCache 将由 GDScript 本地会话创建，作为内存中的数据仓库管理对象使用，不需要场景树生命周期。

## What Changes

- 将抽象 DataRepoCache 的基类从 Node 改为 RefCounted。
- 保持按仓库类型获取仓库的现有抽象契约。

## Capabilities

### Modified Capabilities
- `data-repository`: 管理入口不依赖场景树。

## Impact

修改 `scripts/core/data/DataRepoCache.cs`、数据层文档和对应规范；后续功能可实现会话私有的缓存。
