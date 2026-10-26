## MODIFIED Requirements

### Requirement: 实例存档与完整快照
服务端 SHALL 使用 DataServerBag、DataServerItem 在 `<角色ID>.bag.json` 独立原子保存增加行数、实例 ID、配置 ID、格子位置、数量、独立版本和下一个实例编号，并保存角色 ID 校验归属。旧单文件存档缺失字段 SHALL 为增加行数 0 和空背包。系统 SHALL 拒绝重复实例/格子、越界格子、未知配置、非法数量或编号。选角 SHALL 独立携带只包含背包物品的 BagPb，装备位于公开世界状态；仅本人接收的 RoleItemsChangedNotice SHALL 携带角色 ID 以及同次提交的完整背包与装备快照，移动消息 SHALL 不携带物品列表。RoleCache SHALL 保存独立且只读的 BagPbData 和 ItemPbData，先验证背包、装备与跨列表实例唯一性后原子替换，忽略其他角色及非较新物品提交版本，切角清空。

#### Scenario: 重启和切角
- **WHEN** 发放后重新登录或切换角色
- **THEN** 原角色物品持久保留，其他角色不收到其物品，旧消息不覆盖新会话

#### Scenario: 非法快照
- **WHEN** 当前背包收到重复格子或数量非法的较新快照
- **THEN** 拒绝整个快照并保留原有效数据，普通监听不处理该消息

#### Scenario: 私有背包结构
- **WHEN** 构造并往返序列化 BagPb
- **THEN** 仅包含背包版本与背包格子实例，不再包含装备列表

#### Scenario: 本人物品组合提交
- **WHEN** GM 发放、移动、丢弃、拾取或穿脱成功并产生实际物品变更
- **THEN** 本人收到完整物品组合通知；普通背包操作保持装备版本和内容，穿脱时两侧同时更新，不向其他观察者交付背包
