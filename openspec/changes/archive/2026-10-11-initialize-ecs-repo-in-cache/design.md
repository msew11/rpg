## Context

当前实现由 `WorldState` 先创建 `DataEcsWorld`，再通过 `BindWorld` 交给 Cache，允许 Cache 暂时没有 ECS 仓库。

## Goals / Non-Goals

**Goals:** Cache 初始化后立即有唯一仓库；WorldState 从仓库取得数据；保持每个 LocalSession 独立。

**Non-Goals:** 不设计存档或同一 Cache 下多个 WorldState 的业务语义。

## Decisions

`DataRepoEcsWorld` 无参创建自己的 `DataEcsWorld`。`LocalDataRepoCache` 初始化该仓库，并通过内部只读属性交给 `WorldState`。C# `WorldState` 构造函数要求传入 Cache，然后构造 `EcsWorld(repo)`。Godot 的 GDScript 无法向 C# `[GlobalClass]` 的 `.new()` 传构造参数，因此 Cache 暴露 `CreateWorldState()`，在 C# 内调用带参构造。

## Risks / Trade-offs

GDScript 调用方须改用缓存的创建方法。旧的 `WorldState.new()` 路径不再可用；相关调用方和测试同次迁移。
