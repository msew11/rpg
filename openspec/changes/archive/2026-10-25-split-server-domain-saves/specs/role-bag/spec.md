## MODIFIED Requirements

### Requirement: 实例存档与完整快照
服务端 SHALL 使用 DataServerBag、DataServerItem 在 `<角色ID>.bag.json` 独立原子保存增加行数、实例 ID、配置 ID、格子位置、数量、独立版本和下一个实例编号，并保存角色 ID 校验归属。旧单文件存档缺失字段 SHALL 为增加行数 0 和空背包。系统 SHALL 拒绝重复实例/格子、越界格子、未知配置、非法数量或编号。选角 SHALL 独立携带 BagPb，BagChangedNotice SHALL 携带角色 ID 与完整快照，移动消息 SHALL 不携带物品列表。RoleCache SHALL 保存独立且只读的 BagPbData 和 ItemPbData，先验证整份数据再替换，忽略其他角色及非较新版本，切角清空。

#### Scenario: 重启和切角
- **WHEN** 发放后重新登录或切换角色
- **THEN** 原角色物品持久保留，其他角色不收到其物品，旧消息不覆盖新会话

#### Scenario: 非法快照
- **WHEN** 当前背包收到重复格子或数量非法的较新快照
- **THEN** 拒绝整个快照并保留原有效数据，普通监听不处理该消息
