## MODIFIED Requirements

### Requirement: 实例存档与完整快照
服务端 SHALL 使用 DataServerBag、DataServerItem 在 `<角色ID>.bag.json` 独立原子保存增加行数、实例 ID、配置 ID、格子位置、数量、独立版本和下一个实例编号，并保存角色 ID 校验归属。旧单文件存档缺失字段 SHALL 为增加行数 0 和空背包。系统 SHALL 拒绝重复实例/格子、越界格子、未知配置、非法数量或编号。选角 SHALL 独立携带 BagPb，BagChangedNotice SHALL 携带角色 ID 与完整快照，移动消息 SHALL 不携带物品列表。RoleCache SHALL 保存独立且只读的 BagPbData 和 ItemPbData，先验证整份数据再替换，忽略其他角色及非较新版本，切角清空。

#### Scenario: 重启和切角
- **WHEN** 发放后重新登录或切换角色
- **THEN** 原角色物品持久保留，其他角色不收到其物品，旧消息不覆盖新会话

#### Scenario: 非法快照
- **WHEN** 当前背包收到重复格子或数量非法的较新快照
- **THEN** 拒绝整个快照并保留原有效数据，普通监听不处理该消息

### Requirement: 服务端权威移动提交
MoveItem/MoveItemRt SHALL 放在 bag.proto，显式注册传输方向。请求 SHALL 携带角色 ID、实例 ID、目标格和预期背包版本。服务端 SHALL 校验认证、当前角色、实例存在、目标容量和版本，基于最新角色构造候选背包，校验成功后提交内存并标记背包待写、推送完整背包及返回成功。失败 SHALL 保留存档、缓存、实例编号与版本并显示原因；移动期间 GM、角色移动保存和切角 SHALL 不覆盖最新背包。客户端 SHALL 不预先改写权威缓存，过期拖放不得针对新的格子内容执行。

#### Scenario: 旧版本与非法身份
- **WHEN** 请求角色不符、源实例不存在、目标越界或背包已变化
- **THEN** 返回失败且不保存、不递增版本，UI 保留权威位置并提示

#### Scenario: 校验失败与落地后重启
- **WHEN** 校验失败，或成功移动并完成落地后重启
- **THEN** 前者不提交任何内存变更、不推送成功快照；后者恢复移动、交换和合并后的格子与数量
