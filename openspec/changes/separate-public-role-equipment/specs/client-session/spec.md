## MODIFIED Requirements

### Requirement: 会话缓存权威数据
SessionService SHALL 直接持有 RoleCache，由其维护角色数据，不设置 DataCache 中间容器或独立 BagCache。协议纯数据结构 SHALL 重新组装成独立的同名 PbData 数据类，不保留可变 protobuf 对象引用；所有 PbData 类 SHALL 位于 Session/Data，并在各自文件内提供 FromPb 与 ToPb 双向转换方法，转换时校验数据，输出新的协议对象。RoleWorldProfilePb、RoleWorldStatusPb、RoleWorldPositionPb、RoleWorldEquipmentPb 与私有 RolePb 分别转为不可变 PbData 存入 RoleCache，供当前玩家读取本地组合快照；登录摘要 RoleSummaryPb 转为 RoleSummaryPbData 存入 LoginCache。响应及推送 SHALL 先通过缓存监听按当前角色身份和各部分最后接纳的版本更新，再通知普通监听；世界更新仍经 Action。

本地组合快照 SHALL 保存角色 ID、名称、等级、生命、魔力、经验、性别、逻辑位置、装备及各部分权威版本和确认序号，位置仅保存一份；转回 RoleWorldStatePb 时不得包含私有玩法数据。完整世界推送 SHALL 仅合并资料、生命魔力与位置的较新部分；当前玩家装备 SHALL 只通过选角及私有 RoleItemsChangedNotice 与背包一起原子接纳，独立公开装备推送或后续完整世界推送不得单独覆盖本人装备；RoleChangedNotice SHALL 仅合并私有玩法数据；MoveRt SHALL 只合并位置并核对移动确认序号。无效消息 SHALL 不改变缓存。

#### Scenario: 状态更新
- **WHEN** 服务端返回较新的角色位置状态
- **THEN** 缓存先更新且世界协调器收到可转换为 Action 的消息通知

#### Scenario: 过期状态与其他角色
- **WHEN** 收到旧版本或其他角色状态
- **THEN** 当前角色缓存不会被回滚或替换

#### Scenario: 协议与缓存数据隔离
- **WHEN** 选角响应、移动响应或状态推送更新角色缓存后，其他监听修改原协议对象
- **THEN** 缓存中的 RoleWorldStatePbData 与已有旧快照保持独立，不随协议对象变化

#### Scenario: 数据转回协议
- **WHEN** 将有效 PbData 通过 ToPb 转回协议结构
- **THEN** 世界 ToPb 仅保留可见字段、独立装备并使用当前权威 Position，不包含私有背包，私有数据由 ToRolePb 单独输出；返回对象均与原数据隔离

#### Scenario: 交错局部更新
- **WHEN** 资料、生命魔力、位置与私有 Role 推送按不同顺序到达，随后收到较旧完整世界快照
- **THEN** 四部分独立保留较新值，UI 读取一致的本地组合状态

#### Scenario: 私有领域独立更新
- **WHEN** 可见生命魔力与私有 Role 数据交错到达，随后收到旧完整世界快照
- **THEN** 各部分保留较新值，世界状态不覆盖经验或背包扩容

#### Scenario: 选角缺少私有领域
- **WHEN** 成功选角响应缺少 RolePb、背包或公开装备，或各部分角色 ID 与响应不符
- **THEN** 拒绝整份响应，不留下部分角色或背包缓存

#### Scenario: 主动位置纠偏
- **WHEN** 收到当前角色更高版本的位置推送，移动确认序号未增加
- **THEN** 角色缓存和世界坐标采用新位置

#### Scenario: 本人物品通知先于普通监听
- **WHEN** 收到本人的较新完整物品通知
- **THEN** 先检查身份、两侧内容、版本及跨列表唯一性，全部成功才发布两侧新快照，普通监听读取同次提交的背包与装备

#### Scenario: 本人物品版本回退与冲突
- **WHEN** 收到旧物品提交、较新提交中装备版本回退，或相同装备版本携带不同内容
- **THEN** 旧提交整份忽略；回退或冲突报告错误；均不留下部分背包或装备替换

#### Scenario: 本人的公开装备消息交错
- **WHEN** 本人物品变更前后交错收到包含本人较新装备的公开独立或完整世界推送
- **THEN** 本人装备只随私有组合通知更新；其他较新世界领域仍可独立更新，不产生物品短暂重复或消失

#### Scenario: 组合消息中一侧非法
- **WHEN** 较新本人物品通知中装备或背包非法，或同一实例同时出现在两侧
- **THEN** 拒绝整条消息并保留两侧原快照，普通监听不处理该消息

#### Scenario: 切角清空独立装备
- **WHEN** 返回选角并进入另一角色，旧会话的装备或物品消息晚到
- **THEN** 旧装备与背包同时清空，新角色只从本次成功选角初始化，晚到消息不污染新角色
