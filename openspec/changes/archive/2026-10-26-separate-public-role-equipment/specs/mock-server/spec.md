## MODIFIED Requirements

### Requirement: 独立服务端角色状态
模拟服务端 SHALL 使用独立于客户端缓存和客户端数据类的模型处理登录、选角、移动与角色状态推送，仅共享 protobuf 协议。RoleWorldStatePb 仅由可见资料、生命魔力状态、位置及独立装备组成；经验、升级经验和背包扩容仅由当前玩家私有的 RolePb 承载。SelectRoleRt 将 RolePb 与 BagPb 并列返回；RoleStateNotice 仅返回可见完整快照，MoveRt 仅返回位置。主动资料、状态、位置或私有 Role 数据提交成功后分别允许发送 RoleProfileNotice、RoleStatusNotice、RolePositionNotice 或 RoleChangedNotice；公开装备变化使用 RoleEquipmentNotice；本人的物品提交使用仅本人接收的 RoleItemsChangedNotice，同时携带背包与装备。普通移动不额外给自己发送位置推送。演示账号 SHALL 保留 demo/demo，全新目录从空列表开始，允许显式创建与删除至空列表。

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

#### Scenario: 公开和私有投影
- **WHEN** 从同一服务端角色状态分别生成完整世界状态、独立装备通知及本人物品通知
- **THEN** 前两种载荷只有公开数据，本人物品通知才包含背包，三者装备内容和装备版本一致

#### Scenario: 穿脱通知与普通背包通知
- **WHEN** 当前角色成功穿脱，随后成功移动背包物品
- **THEN** 每次实际物品变更只向本人交付一条完整物品通知；穿脱产生可供观察者消费的独立装备载荷，普通背包移动不产生装备变更载荷

## ADDED Requirements

### Requirement: 装备版本存档兼容
服务端 SHALL 保留背包与装备共同提交、共同落地和现有实例编号语义，同时保存独立装备版本。旧存档缺失装备版本时 SHALL 以原物品版本初始化，已有穿戴列表不变；新角色两个版本为零。读取旧档 SHALL 不立即写盘，下一次正常物品领域落地保存装备版本；失败重试和共同恢复仍适用于背包与装备。

#### Scenario: 旧档保留装备
- **WHEN** 读取存在穿戴列表、没有独立装备版本的旧存档，正常修改并完成落地后重启
- **THEN** 装备版本由原物品版本初始化后按实际装备变化递增，背包、穿戴实例与编号完整保留，不因网络结构拆分丢失装备

#### Scenario: 共同落地失败
- **WHEN** 穿脱已提交内存后，物品领域文件落地失败并重试
- **THEN** 两侧已确认内存和待写状态保留，重试成功后重启恢复同次物品提交，磁盘不出现只有一侧生效的状态
