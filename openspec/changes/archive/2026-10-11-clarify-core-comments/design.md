## Context

`main` 维护 `scripts/core/`。数据仓库抽象已有中文职责注释，ECS 的组件和系统注释为英文，世界类没有类与方法注释。

## Goals / Non-Goals

**Goals:** 使核心类及方法符合 `AGENTS.md` 的中文注释约定。

**Non-Goals:** 调整 ECS 行为、公开接口或数据仓库契约。

## Decisions

- 在声明之前使用简短中文注释，说明类职责、方法结果和关键边界。保持现有 GDScript 语句不变。
- 现有数据仓库注释若已清楚说明职责则保留，避免重复措辞。

## Risks / Trade-offs

- [纯注释编辑误改逻辑] → 审核差异并运行项目检查。
