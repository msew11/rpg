## MODIFIED Requirements

### Requirement: 独立服务端角色状态
模拟服务端 SHALL 使用独立于客户端缓存和客户端数据类的模型处理登录、选角、移动与角色状态推送，仅共享 protobuf 协议。RoleWorldStatePb SHALL 仅由可见资料、生命魔力状态和位置构成；经验、升级经验及背包扩容 SHALL 只出现在当前玩家私有的 RolePb 中。SelectRoleRt SHALL 将 RolePb 与 BagPb 并列返回。RoleStateNotice SHALL 只含可见世界快照；RoleStatusNotice 和 RoleChangedNotice SHALL 分别承载可见状态和私有 Role 领域变化。MoveRt 仍只返回位置，普通移动不重复发送位置推送。演示账号 SHALL 保留 demo/demo，全新目录从空列表开始，允许显式创建与删除至空列表。

#### Scenario: 延迟移动确认
- **WHEN** 选角成功后发送移动请求并推进模拟时间
- **THEN** 按现有延迟返回只含位置、地图、版本和确认序号的 MoveRt，不重复发送 RolePositionNotice

#### Scenario: 主动角色更新
- **WHEN** 服务端主动更新当前角色世界状态
- **THEN** 提交成功后发出只含可见字段的 RoleStateNotice；私有 Role 数据若变化则另发 RoleChangedNotice

#### Scenario: 主动局部更新
- **WHEN** 服务端分别提交当前角色资料、位置、生命魔力状态或私有 Role 数据
- **THEN** 只发出对应领域推送，其他领域保持原值

#### Scenario: 完整世界更新同时改变私有数据
- **WHEN** 完整角色提交同时改变生命魔力和经验
- **THEN** RoleStateNotice 仅包含可见世界状态，另发 RoleChangedNotice 更新当前玩家私有数据
