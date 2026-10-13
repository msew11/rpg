# Proposal

## Why

当前 Debug 面板没有创建区块子实体的入口，无法在运行中的世界里验证 monster 的区块归属。先提供最小的创建能力，为后续独立确定显示和行为留下基础。

## What Changes

- Debug 面板增加创建一个 monster 的按钮。
- 在玩家当前行走区块内随机选择位置，创建带 monster 标记和位置的 ECS 子实体，归属对应的活跃规范区块实体。
- 该实体随现有区块卸载、恢复及保存流程处理；本次不增加显示、移动、战斗或生成规则。

## Capabilities

### New Capabilities

- `debug-monster-spawning`: 定义 Debug 面板创建 monster 实体及其区块归属。

### Modified Capabilities

无。

## Impact

影响 Debug 面板与主场景事件连接、`WorldState` 的实体创建入口、monster 组件及相关世界测试。沿用现有 ECS 和区块持久化机制。
