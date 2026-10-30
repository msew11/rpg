## MODIFIED Requirements

### Requirement: 实例存档与完整快照
服务端 SHALL 在 `<角色ID>.item.json` 数组保存全部持有实例的 InstanceId、ItemId、SlotIndex、Count 和 Equipped；装备扩展数组使用同一 InstanceId，角色单例保存容量和下一个编号。已穿实例 SlotIndex 固定为 0；服务器可保存权威穿戴位置，但 ItemPb 不传递装备槽位；角色和物品不携带信封 revision；客户端本地展示计数不落盘。服务端 SHALL 仅使用当前实体表，不从旧单文件补齐空背包或扩容数据。系统 SHALL 拒绝重复实例/未穿格子、越界格子、未知配置、非法数量或编号。选角 SHALL 独立携带包含全部物品实例的 BagPb，装备位于公开世界状态；仅本人接收的 RoleItemsChangedNotice SHALL 携带角色 ID 以及同次提交的完整背包与装备快照，移动消息 SHALL 不携带物品列表。RoleCache SHALL 保存独立且只读的 BagPbData 和 ItemPbData，先验证实例唯一性、未穿格子唯一性及公开装备与已穿实例的一致性后原子替换，忽略其他角色和旧代次物品消息，切角清空。

#### Scenario: 重启和切角
- **WHEN** 发放后重新登录或切换角色
- **THEN** 原角色物品持久保留，其他角色不收到其物品，旧消息不覆盖新会话

#### Scenario: 非法快照
- **WHEN** 当前背包收到重复格子或数量非法的当前代次快照
- **THEN** 拒绝整个快照并保留原有效数据，普通监听不处理该消息

#### Scenario: 私有背包结构
- **WHEN** 构造并往返序列化 BagPb
- **THEN** 仅包含全部私有物品实例，每个实例携带 equipped，装备实例通过 oneof 携带空 EquipmentPb，不含 revision 或 reserved

#### Scenario: 本人物品组合提交
- **WHEN** GM 发放、移动、丢弃、拾取或穿脱成功并产生实际物品变更
- **THEN** 本人收到完整物品组合通知；普通背包操作保持装备内容，穿脱时两侧同时更新；两侧在同一通知中原子接纳，不向其他观察者交付背包

### Requirement: 服务端权威移动提交
MoveItem/MoveItemRt SHALL 位于 bag.proto 并显式注册方向。请求只携带实例 ID、目标格，当前角色由连接绑定取得，不传预期版本。服务端 SHALL 基于执行时最新状态校验身份、实例和容量，完整候选成功后提交内存、发送完整本人物品通知并回复；失败保留状态和编号。客户端不乐观改写缓存，刷新或切角清除拿起上下文。

#### Scenario: 非法身份与实例
- **WHEN** 尚未控制角色、源实例不存在或目标越界
- **THEN** 返回失败且不修改状态，UI 保留权威位置并提示

#### Scenario: 校验失败与落地后重启
- **WHEN** 校验失败，或成功移动并完成落地后重启
- **THEN** 前者不提交任何内存变更、不推送成功快照；后者恢复移动、交换和合并后的格子与数量
