## MODIFIED Requirements

### Requirement: 会话缓存权威数据
SessionService SHALL 直接持有 RoleCache，由其维护角色数据，不设置 DataCache 中间容器或独立 BagCache。协议纯数据结构 SHALL 重新组装成独立的同名 PbData 数据类，不保留可变 protobuf 对象引用；所有 PbData 类 SHALL 位于 Session/Data，并在各自文件内提供 FromPb 与 ToPb 双向转换方法，转换时校验数据，输出新的协议对象。RoleWorldProfilePb 与 RoleWorldPositionPb SHALL 分别转为不可变 PbData 存入 RoleCache，组合为 RoleWorldStatePbData；登录摘要 RoleSummaryPb SHALL 转为 RoleSummaryPbData 存入 LoginCache。响应及推送 SHALL 先通过缓存监听按当前角色身份和各部分最后接纳的版本更新，再通知普通监听；UI 和世界读取组合的独立快照，世界更新仍经 Action。

RoleWorldStatePbData SHALL 保存角色 ID、名称、等级、生命、魔力、经验、性别、逻辑位置及两部分权威版本和确认序号，位置仅保存一份。完整快照 SHALL 分别合并较新部分；MoveRt SHALL 只合并位置并核对移动确认序号。无效消息 SHALL 不改变缓存。

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
- **THEN** 保留全部业务字段并返回独立对象，修改返回对象不影响原数据，世界状态的坐标使用当前权威 Position

#### Scenario: 交错局部更新
- **WHEN** 资料与位置推送按不同顺序到达，随后收到较旧完整快照
- **THEN** 两部分独立保留较新值，UI 读取一致的组合状态

#### Scenario: 主动位置纠偏
- **WHEN** 收到当前角色更高版本的位置推送，移动确认序号未增加
- **THEN** 角色缓存和世界坐标采用新位置
