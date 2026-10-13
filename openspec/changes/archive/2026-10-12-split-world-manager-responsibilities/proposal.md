# Proposal

## Why

`WorldManager` 同时实现区块实体生命周期、临时存档读写、实体树 JSON 编解码和怪物回环显示查询，使世界边界难以维护。路线图 3c 第 7 项要求将这些独立职责迁出，同时保持现有行为。

## What Changes

- 将活跃区块实体的创建、卸载与临时存档读写归入区块生命周期类。
- 将实体树 JSON 的采集、验证和恢复归入持久化编解码类。
- 将怪物可见位置与回环投影归入只读查询类。
- 世界管理器保留 ECS 调度、会话入口、变化通知和兼容的公开查询；不改变存档格式及游戏行为。

## Capabilities

### Modified Capabilities

- `entity-persistence`：保持区块实体树的落地与恢复语义。
- `continuous-chunk-world`：保持区块跟随、规范 ID 和回环实例行为。
- `debug-monster-spawning`：保持怪物在活跃区块及回环实例中的显示。

## Impact

影响 `scripts/world/` 的职责划分、相关回归测试及架构与路线图文档。
