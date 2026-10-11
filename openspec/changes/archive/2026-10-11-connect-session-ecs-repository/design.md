## Context

`DataEcsWorld` 已将实体数据从 `EcsWorld` 抽离。现有 `WorldState` 默认构造会立即建立世界实体，故会话在创建世界后，把同一数据实例绑定到其 Cache。

## Goals / Non-Goals

**Goals:** 一个 LocalSession 独占一个 Cache；Cache 持有单例 ECS 仓库；ECS 与仓库引用同一数据实例。

**Non-Goals:** 不设计持久化或组件级序列化。

## Decisions

`LocalDataRepoCache : DataRepoCache` 是 `[GlobalClass]`，供 GDScript `.new()` 创建。`RepoWorldEcs : DataRepoSingle<DataEcsWorld>` 只承载一个数据实例。`WorldState.bind_cache` 只允许一次绑定，并使 Cache 注册仓库。组件读写继续由 `EcsWorld` 执行。缓存不进入场景树。

## Risks / Trade-offs

缓存与世界初始化分两步，调用方必须先绑定再开展会话操作；`LocalSession._init` 保证此顺序。尚未设计加载存档时替换数据实例的流程。
