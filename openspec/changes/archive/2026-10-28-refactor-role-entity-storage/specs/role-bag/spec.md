# Spec Delta

## MODIFIED Requirements

### Requirement: 实例存档与完整快照
服务端 SHALL 在 `<角色ID>.item.json` 数组保存全部持有实例的 InstanceId、ItemId、SlotIndex、Count 和 Equipped；装备扩展数组使用同一 InstanceId，角色单例保存容量和下一个编号。已穿实例 SlotIndex 固定为 0；服务器可保存权威穿戴位置，但 ItemPb 不传递装备槽位；角色和物品 revision 仅来自消息信封，不落地。旧单文件存档缺失字段 SHALL 为增加行数 0 和空背包。系统 SHALL 拒绝重复实例/未穿格子、越界格子、未知配置、非法数量或编号。选角 SHALL 独立携带包含全部物品实例的 BagPb，装备位于公开世界状态；仅本人接收的 RoleItemsChangedNotice SHALL 携带角色 ID 以及同次提交的完整背包与装备快照，移动消息 SHALL 不携带物品列表。RoleCache SHALL 保存独立且只读的 BagPbData 和 ItemPbData，先验证实例唯一性、未穿格子唯一性及公开装备与已穿实例的一致性后原子替换，忽略其他角色及非较新物品提交版本，切角清空。

#### Scenario: 重启和切角
- **WHEN** 发放后重新登录或切换角色
- **THEN** 原角色物品持久保留，其他角色不收到其物品，旧消息不覆盖新会话

#### Scenario: 非法快照
- **WHEN** 当前背包收到重复格子或数量非法的较新快照
- **THEN** 拒绝整个快照并保留原有效数据，普通监听不处理该消息

#### Scenario: 私有背包结构
- **WHEN** 构造并往返序列化 BagPb
- **THEN** 仅包含全部私有物品实例，每个实例携带 equipped，装备实例通过 oneof 携带空 EquipmentPb，不含 revision 或 reserved

#### Scenario: 本人物品组合提交
- **WHEN** GM 发放、移动、丢弃、拾取或穿脱成功并产生实际物品变更
- **THEN** 本人收到完整物品组合通知；普通背包操作保持装备内容，穿脱时两侧同时更新；两侧统一使用该次信封版本，不向其他观察者交付背包
