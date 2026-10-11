## Context

GDScript 对 C# `[GlobalClass]` 使用 `WorldState.new(cache)` 会报参数过多；C# 本身能够使用 `new WorldState(cache)`。

## Goals / Non-Goals

**Goals:** 由 WorldState 负责对外创建世界；Cache 不提供世界工厂。

**Non-Goals:** 不改动 ECS 仓库所有权或 C# 带参构造函数。

## Decisions

在 `WorldState` 增加静态 `Create(LocalDataRepoCache)`，其实现调用已有构造函数。GDScript 使用 `WorldState.Create(cache)`，C# 调用方仍可 `new WorldState(cache)`。删除 `LocalDataRepoCache.CreateWorldState()`。

## Risks / Trade-offs

GDScript 无法使用用户最初设想的 `WorldState.new(cache)` 语法，但创建逻辑和入口均在 WorldState，且完整检查验证静态方法可调用。
