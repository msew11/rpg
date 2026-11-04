# Proposal

## Why

客户端攻击循环和预测保存在 WorldService，CombatPose 在 ECS 读模型之外覆盖动作，导致世界状态不完整。将既有战斗状态纳入 ECS，使走近、取消、冷却和动作具有统一来源，并保持服务端权威结算。

## What Changes

- 将持续目标、单次攻击、独立冷却和预测属性放入实体组件，按走近、移动、攻击、动作合成顺序推进。
- WorldService 仅转换输入、权威通知和有序出站事件；保留取消及移动即时发送、移动合并与确认机制。
- 本人和远端攻击统一输出 ECS 只读动作；怪物生命进入组件，保留版本、死亡墓碑与确认后跳字。
- 保持现有玩法、协议和服务端 Actor 结算不变。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ecs-world`：补充客户端战斗状态、系统调度、网络事件边界、权威生命和完整动作快照契约；允许输入阶段即时处理 Action 以保持既有发送顺序。

## Impact

影响 src/World、Session 战斗通知交接、相关测试及架构/路线图文档。无新依赖，无协议和服务端持久化变更。
