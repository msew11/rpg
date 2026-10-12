# Design

## Context

`WorldMath` 提供坐标计算，`WorldEntityTreeCodec` 提供实体树编解码，`WorldMonsterQuery` 提供只读显示位置查询，三者均为静态无状态类。调用方按 C# 类型引用它们，没有源文件路径依赖；Godot 为三个 C# 文件生成了稳定 UID。

## Goals / Non-Goals

**Goals:** 在 `scripts/world/utils/` 集中世界内部工具类，并保持编译、存档、区块回环和怪物显示行为。

**Non-Goals:** 不重命名类、不改变公开接口或计算规则，不移动持有会话状态的 `WorldChunkLifecycle`。

## Decisions

- 用 Git 重命名移动三个 `.cs` 文件及其 `.uid`，保留 UID 内容和 C# 类型名。
- 顶层 `scripts/utils/` 继续存放 GDScript 展示几何工具；`scripts/world/utils/` 存放 C# 世界内部工具。

## Risks / Trade-offs

- 文件路径变化可能影响 Godot 导入；运行完整项目检查验证脚本导入、世界和存档场景。
