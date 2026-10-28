# Proposal

## Why

公开装备只需要展示配置编号；物品实例、穿戴状态和未来装备属性应留在私有物品领域。角色列表当前读取全部角色和背包，需要改为摘要快照优先、选角后按需加载实体表。

## What Changes

- **BREAKING** 合并 equipment.proto 到 bag.proto，删除 EquippedItemPb 和 BagPb reserved；公开装备及选角摘要使用 repeated int32 equipped_items。
- ItemPb 增加 equipped 与包含空 EquipmentPb 的 oneof；已穿实例 slot_index 固定为 0，客户端按配置推导展示槽位。
- **BREAKING** revision 仅由 EnvelopePb 传递，每次消息递增，不再作为角色和物品数据落地；地图存储及对象版本保持原有契约。
- 角色采用 RoleEntity 单例、ItemEntity / EquipmentEntity 多例及对应 EntityCache；物品与装备按 InstanceId 关联，分别写 role/item/equipment JSON。
- 增加 role_summary.json 展示快照；登录只加载摘要，选角后加载该角色其他实体，保留延迟落地、旧档迁移和事务恢复。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-network`: 装备协议归并与消息信封版本。
- `mock-server`: 实体表存储、摘要登录与选角加载。
- `role-bag`: 私有完整物品、穿戴标记与装备扩展载荷。
- `role-equipment`: 公开配置编号展示与私有原子穿脱。
- `client-session`: 使用消息版本及摘要装备数据。
- `role-management`: 选角预览展示角色已有装备。

## Impact

影响 proto 和生成代码、Server 仓库及实体缓存、Session 数据转换、World/UI 装备消费者、角色选择预览、回归测试及架构/路线图。不改变地图落地格式，不添加装备属性或玩法。
