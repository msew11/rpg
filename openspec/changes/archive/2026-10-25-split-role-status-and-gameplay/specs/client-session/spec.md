## MODIFIED Requirements

### Requirement: 模拟登录与选角
客户端 SHALL 通过请求协议将用户名和密码交给模拟服务端；成功后 LoginRt 返回当前未删除角色的 RoleSummaryPb 列表（允许为空），仅包含角色 ID、名称、等级、性别与头型；全新存档首次登录 SHALL 返回空角色列表，不自动生成演示角色。选角 SHALL 复用该会话并在请求前清空角色数据、隔离旧网络代次；SelectRoleRt 成功时 SHALL 并列返回可见 RoleWorldStatePb、私有 RolePb 与 BagPb，全部有效后才初始化缓存、世界和界面；失败时只清理角色数据，保留账号会话与登录摘要。

#### Scenario: 选角加载世界状态
- **WHEN** 登录后选择角色
- **THEN** 返回该角色最新的世界、私有 Role 和背包数据，经验与背包扩容不会出现在世界状态

#### Scenario: 演示账号登录
- **WHEN** 输入 `demo/demo`
- **THEN** 在 SessionService 内建立账号状态，LoginCache 保存 RoleSummaryPbData 列表供选角界面读取，RoleCache 尚未初始化；登录协议不返回生命、魔力、经验、坐标、版本或移动确认序号等世界状态

#### Scenario: 首次登录为空列表
- **WHEN** 使用全新存档登录演示账号并重启后再次登录
- **THEN** 登录成功且角色列表均为空，仍可进入创建流程

#### Scenario: 错误凭据
- **WHEN** 凭据不匹配演示账号
- **THEN** 显示失败反馈且不建立有效账号会话；当前尝试的空缓存保持未认证且无角色摘要

#### Scenario: 角色切换
- **WHEN** 在一名角色的会话后选择另一角色
- **THEN** 保留同一账号 SessionService 与 LoginCache，清空 RoleCache 后加载新角色快照，旧消息不更新新角色数据，角色设置相互独立

#### Scenario: 选角失败
- **WHEN** 角色不存在或角色存档无法读取
- **THEN** 展示错误并清理待选角色数据，保留账号会话与登录摘要，不创建世界或 HUD

### Requirement: 会话缓存权威数据
RoleCache SHALL 分别校验并持有不可变的世界资料、生命魔力状态、位置及私有 RolePbData，按各自版本接纳当前角色更新；RolePb 不得序列化进 RoleWorldStatePb。选角响应 SHALL 原子初始化四部分和背包；完整世界推送 SHALL 只更新可见部分，RoleChangedNotice SHALL 只更新私有玩法数据。UI 可读取组合的本地角色视图，世界更新仍经 Action。无效消息 SHALL 不改变缓存。

#### Scenario: 协议与缓存数据隔离
- **WHEN** 选角响应或任一局部推送更新角色缓存后，其他监听修改原协议对象
- **THEN** 缓存与已有旧快照保持独立，不随协议对象变化

#### Scenario: 状态更新
- **WHEN** 服务端返回较新的角色位置状态
- **THEN** 缓存先更新且世界协调器收到可转换为 Action 的消息通知

#### Scenario: 过期状态与其他角色
- **WHEN** 收到旧版本或其他角色状态
- **THEN** 当前角色缓存不会被回滚或替换

#### Scenario: 数据转回协议
- **WHEN** 将有效 PbData 通过 ToPb 转回协议结构
- **THEN** 世界 ToPb 仅保留可见字段并使用当前权威 Position，私有数据由 ToRolePb 单独输出；返回对象均与原数据隔离

#### Scenario: 交错局部更新
- **WHEN** 世界资料、生命魔力、位置和私有 Role 变化交错到达
- **THEN** 各部分独立保留较新值，世界消息不能覆盖私有数据，UI 读取一致的本地组合视图

#### Scenario: 私有领域独立更新
- **WHEN** 可见生命魔力与私有 Role 数据交错到达，随后收到旧完整世界快照
- **THEN** 各部分保留较新值，世界状态不覆盖经验或背包扩容

#### Scenario: 选角缺少私有领域
- **WHEN** 成功选角响应缺少 RolePb 或其角色 ID 与世界状态不符
- **THEN** 拒绝整份响应，不留下部分角色或背包缓存

#### Scenario: 主动位置纠偏
- **WHEN** 收到当前角色更高版本的位置推送，移动确认序号未增加
- **THEN** 角色缓存和世界坐标采用新位置
