# client-network Specification

## Purpose

定义客户端发送请求与接收响应、服务器推送的统一契约，让本地模拟服务端也经过真实的 protobuf 字节边界，并使界面和世界无需依赖底层传输实现即可消费协议事件。
## Requirements
### Requirement: protobuf 协议字节边界
客户端与服务器 SHALL 通过 protobuf 字节交换数据。唯一源为 proto/，按登录、角色、世界、地图、物品及 GM 业务分类，内部消息放 actor/。请求无后缀、回复以 Rt 结尾、推送以 Notice 结尾、数据及枚举以 Pb 结尾。生成 C# 位于 src/Protocol/Generated，内部生成代码位于 src/Server/ActorProtocol/Generated；类型 SHALL 显式注册方向，未知或非法载荷 SHALL 不更新缓存。

#### Scenario: 请求响应和主动推送
- **WHEN** 客户端发送登录、选角、创建、删除、移动或 GM 请求，或服务端主动推送世界帧及本人物品组合
- **THEN** 双向消息都经过序列化及解析，保留消息类型和数据，接收方不持有发送方对象

#### Scenario: 非法消息
- **WHEN** 收到未知类型、错误方向或无法解析的字节
- **THEN** 报告可诊断错误且不分发业务消息

#### Scenario: 装备消息方向
- **WHEN** 穿脱请求、穿脱结果、世界帧公开装备增量和私有物品通知通过显式协议表交换
- **THEN** 请求仅允许客户端发送，结果及两种通知仅允许服务端发送；错误方向被拒绝，原 BagChangedNotice 不再作为有效消息注册

#### Scenario: 删除字段不复用
- **WHEN** 检查删除旧字段后的 BagPb、ItemPb 与穿脱请求描述
- **THEN** 原有实例和请求字段编号保持不变；BagPb 明确移除 reserved 声明且删除的 revision 字段号不分配给新字段，生成代码与协议一致

#### Scenario: 装备协议归并
- **WHEN** 检查新版 BagPb 与装备协议描述
- **THEN** BagPb 无 reserved、无 revision；equipment.proto 和 EquippedItemPb 已移除，所有装备协议由 bag.proto 声明

### Requirement: 队列收发与会话隔离
发送 SHALL 入队而不直接调用监听；固定帧 SHALL 在登录阶段及角色阶段推进通信。结束角色会话 SHALL 使其待发送请求、延迟响应与推送失效，切回同一角色也不得接收旧会话消息。

#### Scenario: 登录前推进
- **WHEN** 尚未建立角色会话时提交登录请求
- **THEN** 发送调用本身不触发响应，后续固定帧交付登录结果

#### Scenario: 同角色新会话
- **WHEN** 角色有延迟移动响应待返回时结束会话并再次选择相同角色
- **THEN** 原响应不更新新会话缓存也不通知新 UI

### Requirement: 信封消息版本
EnvelopePb SHALL 仅包含 generation、request_id、message_type、payload。请求编号非零，回复沿用请求编号，通知为零；代次隔离旧结果。角色和物品数据 SHALL 不携带信封 revision，物品请求不传 expected_revision；物品组合按传输顺序原子接纳。世界帧以 sequence 检查连续性，地图对象 revision 保留存储含义。

#### Scenario: 连续消息与重启
- **WHEN** 多条请求、响应或通知连续发送并切换会话，随后重建发送端
- **THEN** 请求编号用于响应关联，通知为零，旧代次不进入新会话，不从角色文件恢复网络状态

#### Scenario: 嵌套物品数据
- **WHEN** 一条本人物品通知通过字节边界解析
- **THEN** Bag 和公开 Equipment 作为同一完整组合校验后一次替换，二者不携带 revision 字段
