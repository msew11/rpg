# server-v2-actor-protocol Specification

## Purpose

定义 V2 Actor 之间的内部协议组织与对象移交边界，以独立生成的 protobuf 消息支撑 Handler 内只读跨分片查询，并保持客户端协议、存档模型和各服务生命周期隔离。

## Requirements

### Requirement: 内部协议独立生成
内部协议 SHALL 以 proto3 按接收方模块组织在 protov2/actor，使用 rpg.v2.actor package 与 Rpg.ServerV2.ActorProtocol 命名空间，生成到 src/ServerV2/ActorProtocol/Generated。请求 SHALL 使用动作名，响应追加 Rt，结构和枚举使用 Pb；文件依次组织请求响应、推送、枚举结构，无内容部分省略。内部协议默认 SHALL 不导入外部业务协议，公共生成规则 SHALL 排除内部目录，生成类型仅编译一次。

#### Scenario: 构建内外协议
- **WHEN** 构建包含 world.proto 与 role.proto 的项目
- **THEN** 内部完整消息名以 rpg.v2.actor 开头，生成类型只出现在内部目录且无重复编译，外部协议保留原命名空间

### Requirement: 内部消息独占移交
进程内调用 SHALL 直接传递生成 proto，不使用客户端信封、额外序列化、中间请求结果包装或逐请求手写接口绑定。请求入队后发送方 SHALL 不再修改，回复返回后接收 Handler SHALL 不留存并修改；消息 SHALL 不携带 Context、Repo 或工作委托，存档 SHALL 保持独立 Data 模型。

#### Scenario: 直接请求与类型校验
- **WHEN** Actor Ask 内部请求并指定响应类型
- **THEN** 目标 Handler 接收原 proto、直接返回 proto，响应类型不匹配向调用者报错且后续邮箱仍可工作

### Requirement: Role 查询所属地图对象数
Role SHALL 接收 GetRoleWorldObjectCount，在所属 Async Handler 中按当前角色 MapId 定位 World 并发送 GetWorldObjectCount，返回 GetRoleWorldObjectCountRt。World 本地 Handler SHALL 以 GetWorldObjectCountRt 的 int32 count 返回完整对象数量。目标加载或处理错误 SHALL 传播到源调用者，源 Actor 等待期间仍独占同一串行回合，独立 Actor SHALL 可继续工作。

#### Scenario: 查询已有对象
- **WHEN** Role 所在地图完整加载多个对象后收到查询
- **THEN** Role 返回该地图的对象数量，不改变任何存储快照或写回基线

#### Scenario: 等待和错误隔离
- **WHEN** World 加载或 Handler 阻塞并随后失败
- **THEN** Role 后续消息等待本回合，其他 Actor 可工作，错误传播后源邮箱继续处理

### Requirement: 客户端拒绝内部协议
内部消息 SHALL 不注册客户端 MessageCodec。客户端伪造内部完整类型名 SHALL 被拒绝，默认登录 SHALL 继续返回尚未实现。

#### Scenario: 外部投递内部请求
- **WHEN** 客户端通过有效信封发送内部查询消息
- **THEN** 返回关联的未知消息错误，不分发到内部 Handler，后续外部登录仍返回未实现
