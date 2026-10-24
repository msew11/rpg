## MODIFIED Requirements

### Requirement: protobuf 协议字节边界
客户端与模拟服务端 SHALL 通过 protobuf 字节交换数据。请求消息无后缀，响应以 Rt 结尾，推送以 Notice 结尾，纯数据结构与枚举以 Pb 结尾；声明放在 proto，公共信封放在 message.proto，角色共享枚举、角色摘要、角色世界状态、选角和创建/删除协议放在 role.proto，登录协议放在 login.proto，移动与推送放在 world.proto，生成 C# 放在 Net/Message。协议类型 SHALL 通过显式注册路由，未知类型或非法载荷 SHALL 报告错误而不更新缓存。

#### Scenario: 请求响应和主动推送
- **WHEN** 客户端发送登录、选角、创建、删除或移动请求，或服务端主动推送角色状态
- **THEN** 双向消息都经过序列化及解析，保留消息类型和数据，接收方不持有发送方对象

#### Scenario: 非法消息
- **WHEN** 收到未知类型、错误方向或无法解析的字节
- **THEN** 报告可诊断错误且不分发业务消息
