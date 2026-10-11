## Context

`develop` 中的本地缓存创建 ECS 单例仓库，`WorldState` 将仓库交给世界。核心现只接收 `IEcsWorldData`。

## Goals / Non-Goals

**Goals:** 保留会话独占与世界共享数据实例行为，同时让具体数据归开发层所有。

**Non-Goals:** 存档格式及其他仓库功能。

## Decisions

- `EcsWorldData` 实现 `IEcsWorldData` 与 `IDataRepoData`，沿用现有状态结构。
- `EcsWorldDataRepo : DataRepoSingle<EcsWorldData>` 由缓存唯一持有。
- `WorldState` 传入 `cache.WorldRepo.Data`；核心不接收仓库。
- 更新类型查询与文档，不为旧类名添加别名。

## Risks / Trade-offs

具体类名变更会影响 C# 调用方；仓库内现有引用必须全部迁移。
