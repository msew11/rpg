# Design

## Context

`WorldManager` 按 C# 类型构造 `WorldChunksView` 和 `WorldDebugView`；GDScript 与测试按 Godot 全局类名使用它们，没有源文件路径引用。两个视图的公开接口已由世界查询规格约束。

## Goals / Non-Goals

**Goals:** 集中组件只读视图文件，保持 Godot 类型注册、查询隔离和现有调用行为。

**Non-Goals:** 不修改视图实现、类型名、API、组件或世界管理器逻辑。

## Decisions

- 用 Git 重命名将两个 `.cs` 及其 `.uid` 移至 `scripts/world/compviews/`，保持文件内容与 UID 不变。
- `scripts/world/components/` 继续存放可变 ECS 组件；`compviews/` 存放面向调用方的只读组件视图。

## Risks / Trade-offs

- Godot 在文件移动后需重新导入全局类；运行完整项目检查覆盖导入、C# 构建和 GDScript 视图调用。
