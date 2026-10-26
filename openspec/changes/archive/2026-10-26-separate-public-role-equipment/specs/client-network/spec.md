## MODIFIED Requirements

### Requirement: protobuf 协议字节边界
客户端与模拟服务端 SHALL 通过 protobuf 字节交换数据。请求消息无后缀，响应以 Rt 结尾，推送以 Notice 结尾，纯数据结构与枚举以 Pb 结尾；声明放在 proto，公共信封放在 message.proto，角色共享枚举、角色摘要、角色世界状态、选角和创建/删除协议放在 role.proto，登录协议放在 login.proto，移动与角色状态推送放在 world.proto，背包数据及仅本人物品组合推送放在 bag.proto，装备快照、装备槽位与穿脱请求响应放在 equipment.proto，独立公开装备推送放在 world.proto，GM 请求响应放在 gm.proto，生成 C# 放在 Net/Message。协议类型 SHALL 通过显式注册路由，未知类型或非法载荷 SHALL 报告错误而不更新缓存。

#### Scenario: 请求响应和主动推送
- **WHEN** 客户端发送登录、选角、创建、删除、移动或 GM 请求，或服务端主动推送角色状态、独立装备及本人物品组合
- **THEN** 双向消息都经过序列化及解析，保留消息类型和数据，接收方不持有发送方对象

#### Scenario: 非法消息
- **WHEN** 收到未知类型、错误方向或无法解析的字节
- **THEN** 报告可诊断错误且不分发业务消息

#### Scenario: 装备消息方向
- **WHEN** 穿脱请求、穿脱结果、公开装备通知和私有物品通知通过显式协议表交换
- **THEN** 请求仅允许客户端发送，结果及两种通知仅允许服务端发送；错误方向被拒绝，原 BagChangedNotice 不再作为有效消息注册

#### Scenario: 删除字段不复用
- **WHEN** 检查新版 BagPb 与穿脱请求的协议描述
- **THEN** BagPb 的原装备字段编号 3 及名称 equipment 保留为 reserved；穿脱请求保留原字段号并增加预期装备版本，生成代码与协议声明一致
