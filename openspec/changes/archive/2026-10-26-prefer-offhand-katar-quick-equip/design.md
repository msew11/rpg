# Design

## Context
BagControl.AutoEquip使用静态推荐槽，拳刃默认主手；服务器已有双手拳刃与占用槽原子替换能力。

## Goals / Non-Goals
实现用户明确的快捷选槽规则，不修改普通点击的显式槽位和其他武器推荐槽。

## Decisions
在AutoEquip拿取当前权威Bag快照后，若待穿物品为Weapon/Katar且主手配置也为Weapon/Katar，则将目标改为OffHand；副手为空、拳套或其他装备均按既有服务器替换规则处理。按分类而非1017编号判断。

## Risks / Trade-offs
不乐观更新，仍携带同份快照版本拒绝并发过期请求；快捷替换只变化目标槽。
