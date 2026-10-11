## Context

当前 DataRepoCache 是抽象 Node，但不使用场景树 API。会话数据管理需要由 GDScript 持有并在引用结束时释放。

## Goals / Non-Goals

**Goals:** 保持 GDScript 可访问的 Godot 对象身份，并去除场景树依赖。

**Non-Goals:** 此次不实现具体缓存、仓库存取或存档。

## Decisions

DataRepoCache 改继承 RefCounted。现有 GetRepo(Type) 抽象方法不变。RefCounted 提供 Godot 对象互操作与引用计数生命周期，无须挂入场景树。

## Risks / Trade-offs

未来若需要节点通知，具体实现应由外部节点协调，不能依赖缓存自身的 Node 回调。
