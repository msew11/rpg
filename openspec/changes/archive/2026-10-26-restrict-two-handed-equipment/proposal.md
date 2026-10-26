# Proposal

## Why

当前允许拳刃独立占主副手、飞刀仅占副手且双手武器可搭配盾牌，与用户确定的新持握规则不符。需要统一穿戴约束，并让一件主手拳刃或飞刀显示完整双手外观。

## What Changes

- **BREAKING**：剑仅主手、盾牌仅副手，只有普通剑与盾牌可同时穿戴；其余已支持武器仅主手并排斥副手装备。
- 拳刃、飞刀只保存一个主手实例，左右手使用各自原生素材，不复制装备实例。
- 移除拳刃快捷补副手逻辑，服务器和公开快照共同校验组合。
- 兼容旧穿戴记录，保留物品与实例；同步正式预览、世界、逐帧审查、测试和文档。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `equipment-config`：主副手分类、双手占用和成对外观判定。
- `role-equipment`：组合校验、原子穿脱、快捷穿戴及旧装备兼容。
- `client-actor-presentation`：主手拳刃与飞刀驱动左右手原画，审查覆盖新组合。

## Impact

影响 EquipmentSlotUtil、DataServerBagRules、MockServer 选角兼容、RoleWorldEquipmentPbData、BagControl、ActorView 及其测试和审查工具；无需修改 protobuf 字段或装备 JSON 结构。
