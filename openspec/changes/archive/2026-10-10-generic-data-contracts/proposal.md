# Proposal

## Why

后续功能需要稳定的数据读取与更新方式，但 main 现在不应确定游戏数据模型或存储提供方。先定义并验证一套小型契约，可让后续功能按需实现提供方。

## What Changes

- 定义通用键值数据源的存在性检查、读取、写入和变化通知契约。
- 用测试替身验证契约，说明提供方必须遵守的行为。
- 正式存储、地图结构和玩法状态留给后续功能。

## Capabilities

### New Capabilities

- `data-access`：通用数据源操作与有效变化通知。

### Modified Capabilities

无。

## Impact

新增 `scripts/data/` 中的 GDScript 契约、`tests/` 中的契约测试和数据层文档，并更新架构与路线图。不修改运行时场景，也不加入正式提供方。
