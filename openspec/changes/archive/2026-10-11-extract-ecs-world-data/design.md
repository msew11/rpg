## Context

EcsWorld 当前直接保存实体状态，未来会话私有仓库需要持有这一状态。

## Goals / Non-Goals

**Goals:** 将实体数据抽取为一个 `IDataRepoData`，不改变现有实体 API 行为。

**Non-Goals:** 不实现仓库缓存、持久化、增量追踪或系统恢复。

## Decisions

`DataEcsWorld` 放在 `scripts/core/data/`，包含下一个实体 ID、有效实体集合和组件索引。`EcsWorld` 保留默认构造，并提供接收数据对象的构造；两者直接操作同一实例。系统注册和调度状态仍是 `EcsWorld` 的运行时状态。

## Risks / Trade-offs

运行时组件索引含 `Type` 与 Godot 对象，不直接构成稳定存档格式；后续由具体仓库收集可持久化内容。
