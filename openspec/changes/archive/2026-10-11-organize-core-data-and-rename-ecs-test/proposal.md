## Why

数据仓库抽象目前直接位于 `scripts/core/`，与 ECS 使用子目录组织的方式不一致。ECS 验收脚本名中的 `contract` 可以去掉，保留 `test_` 前缀和 `ecs` 主题即可清楚表达用途。

## What Changes

- 将两个数据仓库抽象脚本及其 Godot UID 移到 `scripts/core/data/`。
- 将 `test_ecs_contract.gd` 及 UID 改名为 `test_ecs.gd`，同步类名、检查入口和文档。
- 保留现有运行行为和 OpenSpec 需求，故不创建规范增量。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。

## Impact

影响核心脚本路径、测试入口和相关文档。Godot UID 随文件移动，现有全局类名 `DataRepo`、`DataRepoCache` 保持可用。
