## Context

此前的核心注释 change 已覆盖顶层类及方法；`EcsWorld` 的类属性和 `tests/core/test_ecs.gd` 的内部类与属性尚未说明。核心脚本当前没有独立的 `const` 声明，`DataRepo.StorageKind` 是已有的枚举常量。

## Goals / Non-Goals

**Goals:** 补全现有核心声明的注释，并将相同范围写入长期约定。

**Non-Goals:** 为方法内局部变量逐一添加注释，或改变测试和运行时代码行为。

## Decisions

- 每个内部类、常量、类属性和方法声明前放一条简短中文注释。类属性按其在契约或测试中承担的角色说明，避免仅翻译名称。
- 对枚举及其成员分别说明整体用途和每个值的含义。当前无独立 `const`，未来新增时遵守同一规则。
- 核心测试与核心脚本一并覆盖，因为核心测试现已归 `main` 维护，且包含内部类。

## Risks / Trade-offs

- [注释编辑意外改变 GDScript 语法] → 对照差异，运行 Godot 导入、场景和 ECS 测试。
