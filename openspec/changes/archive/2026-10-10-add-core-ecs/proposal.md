# Proposal

## Why

后续功能需要一套与具体玩法无关的 ECS 基础能力，以组合组件并按系统更新实体。当前核心目录没有实体、组件和系统的统一生命周期接口。

## What Changes

- 在 `scripts/core/ecs/` 增加可运行的内存 ECS：实体创建与销毁、按脚本类型附加组件、组件查询和系统调度。
- 增加无界面契约测试及简短使用说明，并纳入项目检查。
- 不引入具体玩法组件、场景节点或持久化策略。

## Capabilities

### New Capabilities

- `ecs-runtime`: 实体、组件、查询和系统执行的通用运行时行为。

### Modified Capabilities

无。

## Impact

新增核心 GDScript 脚本、测试和文档，扩展 `tools/check.ps1`。不改变现有数据源契约或主场景。
