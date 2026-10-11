## Why

核心数据仓库需要编译期泛型与接口约束，现有 GDScript 无法表达；ECS 核心也需要迁入相同的 C# 运行环境。

## What Changes

- **BREAKING** 将 `scripts/core/data/` 和 `scripts/core/ecs/` 的 GDScript 类改为 C#，保留既有 ECS 行为。
- 为数据仓库增加数据接口与泛型基类，由编译器约束仓库承载的数据类型。
- 使用 Godot 4.7.2 .NET 版编辑器、.NET 项目及 C# 核心验收测试。
- 更新核心文档和检查入口；具体游戏数据仓库与玩法组件仍不属于 `main` 的核心范围。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `data-repository`: 用 C# 泛型和数据接口声明仓库承载类型。
- `ecs-runtime`: 用 C# 类型作为组件标识并提供类型安全的组件访问。
- `core-code-documentation`: 将核心中文注释约定应用于 C# 类与成员。

## Impact

影响 `scripts/core/`、`tests/core/`、`tools/check.ps1`、`project.godot`、核心文档与 OpenSpec 契约。`develop` 现有 GDScript ECS 子类无法继承 C# 基类，后续须经单独变更迁移并通过 PR 同步核心。
