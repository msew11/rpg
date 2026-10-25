# mock-server Specification

## Purpose

定义进程内模拟服务端的角色请求处理与数据落地能力，维持现有演示账号及延迟移动校正，并确保服务端拥有独立的数据模型、按角色隔离的存档和可诊断的持久化失败反馈。
## Requirements
### Requirement: 独立服务端角色状态
模拟服务端 SHALL 使用独立于客户端缓存和客户端数据类的模型处理登录、选角、移动与角色状态推送，仅共享 protobuf 协议。RoleWorldStatePb 仅由可见资料、生命魔力状态及位置组成；经验、升级经验和背包扩容仅由当前玩家私有的 RolePb 承载。SelectRoleRt 将 RolePb 与 BagPb 并列返回；RoleStateNotice 仅返回可见完整快照，MoveRt 仅返回位置。主动资料、状态、位置或私有 Role 数据提交成功后分别允许发送 RoleProfileNotice、RoleStatusNotice、RolePositionNotice 或 RoleChangedNotice；普通移动不额外给自己发送位置推送。演示账号 SHALL 保留 demo/demo，全新目录从空列表开始，允许显式创建与删除至空列表。

#### Scenario: 延迟移动确认
- **WHEN** 选角成功后发送移动请求并推进模拟时间
- **THEN** 按现有延迟返回只含位置、地图、版本和确认序号的 MoveRt，不重复发送 RolePositionNotice

#### Scenario: 主动角色更新
- **WHEN** 服务端主动更新当前角色状态
- **THEN** 更新成功提交内存后发出只含可见字段的 RoleStateNotice；私有 Role 数据若变化则另发 RoleChangedNotice

#### Scenario: 主动局部更新
- **WHEN** 服务端分别提交当前角色资料、位置、生命魔力或私有 Role 数据
- **THEN** 只发出对应局部推送，其他部分状态保持不变

#### Scenario: 完整世界更新同时改变私有数据
- **WHEN** 完整角色提交同时改变生命魔力和经验
- **THEN** RoleStateNotice 仅包含可见世界状态，另发 RoleChangedNotice 更新当前玩家私有数据

### Requirement: JSON 角色持久化
服务端 SHALL 在 user://server 使用 `<角色ID>.role.json` 和 `<角色ID>.bag.json` 分别保存角色及背包领域。角色移动与删除只提交 role，GM 和物品移动只提交 bag；单领域正常提交 SHALL 不读取或重写另一领域。请求校验成功后 SHALL 提交内存并响应或推送，文件由 server-write-back 的检测节点或关闭节点延迟落地。创建、迁移及同时变更两领域 SHALL 在失败时回滚，中断后访问前恢复未提交状态。全新目录保持空列表，删除保留墓碑且编号不复用。旧 `<角色ID>.json` SHALL 可读取，首次保存迁移并保留旧文件；新格式存在时不得回退旧数据，分域缺失、损坏或身份不符 SHALL 明确报错。

#### Scenario: 重启与角色隔离
- **WHEN** 角色移动确认成功并完成落地后重建服务端并重新选角
- **THEN** 恢复最后确认的角色状态，其他角色数据保持独立

#### Scenario: 存档损坏
- **WHEN** 首次加载读取到损坏、不合法或身份不符的角色文件
- **THEN** 返回明确错误且不覆盖原文件

#### Scenario: 保存失败
- **WHEN** 检测节点无法写入存储
- **THEN** 保留已确认内存及待写标记，原有效文件保持原值，报告错误并在后续节点重试

#### Scenario: 按领域写入
- **WHEN** 角色移动、GM 发放或物品移动成功
- **THEN** 检测节点只有对应 role 或 bag 文件发生写入，另一领域文件及其 IO 日志保持不变

#### Scenario: 旧文件迁移与墓碑优先
- **WHEN** 首次修改旧单文件角色并完成落地，之后重启或删除
- **THEN** 生成完整分域文件且保留旧文件，背包与属性保持，旧文件不覆盖新领域也不恢复已删除角色

#### Scenario: 多文件中断与损坏隔离
- **WHEN** 创建或迁移中途失败或进程中断，或分域文件缺失、损坏及角色 ID 不符
- **THEN** 磁盘不提交部分角色，缓存保留待写状态，后续访问先恢复未提交状态；无回滚记录的分域损坏明确报错，不回退旧备份

### Requirement: 角色地图身份持久化
服务端 SHALL 为角色保存正整数 MapId；新角色使用配置 initialMapId，旧存档缺失字段兼容原地图 1，读取不立即写盘，下次正常角色领域保存落地。选角和角色状态协议 SHALL 包含所属地图，移动目标 SHALL 在当前地图有效范围内。

#### Scenario: 旧角色迁移
- **WHEN** 读取无 MapId 的旧角色并正常保存角色领域
- **THEN** 读取时属于地图 1 且不改文件，保存后明确记录地图 1

#### Scenario: 指定初始地图
- **WHEN** 初始地图配置为合法的另一数值 ID 并创建角色
- **THEN** 新角色使用该地图且客户端按此身份选择静态地图和动态订阅

#### Scenario: 越界移动
- **WHEN** 请求超出所属地图的移动坐标
- **THEN** 请求失败，不改变移动目标、角色状态或 AOI
