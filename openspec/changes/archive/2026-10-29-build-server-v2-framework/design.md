# Design

## Context

见 proposal。旧 Server 同时承载现有客户端；rpg.csproj 已通过 Grpc.Tools 生成旧协议。当前工作区另有未提交的角色创建重构，须保留且不纳入本次提交。

## Goals / Non-Goals

目标：建立可测试且分类清楚的 V2 基础链路，实现既定 Actor 思想、账号分片、连接绑定与独立协议。
不做：真实认证、游戏 Handler、真实网络、文件 IO、断线续接、重复登录限制、旧数据迁移及默认客户端切换。

## Decisions

- Runtime 只负责 Channel 单消费者邮箱、类型化 Ask/Tell、状态与排空；使用 ActorRef 隔离实现，ActorDirectory 在短锁下唯一创建分片。消息使用数据记录，不传操作委托或可写上下文。
- Modules/Login、Role、World 各自拥有 Actor、上下文与处理器注册。登录按规范 username，角色和地图按正整数 ID；模块初始状态无 IO。处理器只接收上下文与类型化消息，注册显式且在创建 Actor 时冻结。
- Connections 每连接拥有串行入口队列、generation、username 与独立输出队列；短锁保护接纳、重置、绑定和输出，锁不跨 await。连接关闭立即失效，仍排空已接纳回合；generation 重置清旧输出且保留账号绑定。
- Protocol 独立做显式方向注册、protobuf 解码与内部不可变数据转换；Routing 定位账号 Actor 并把类型化结果映射回响应。信封为 generation/request_id/message_type/payload，错误统一 ErrorRt，当前不加发送序号或业务快照版本。请求编号由发送端管理，不实现自动去重。
- 首次 Login 先清除该连接旧账号绑定，成功才重新绑定；完成时必须再次核对连接仍存在且 generation 未变。账号操作通过不可变 Session 快照路由，外部不取得可写会话。相同账号可有多个连接，账号排他策略留待认证。
- username 使用 Trim().ToLowerInvariant()，拒绝控制字符、Windows 非法文件名字符、尾部句点、空账号及设备保留名；不做 Unicode 兼容折叠。Storage 只定义账号摘要数据及仓库接口；文件名规范为 username.role_summary.json。
- Hosting/MockServerService 继承现有 Service，公开构造只注入 ConfigService，并持有它供后续业务构造使用；内部纯 .NET ServerRuntime 便于无引擎测试。NetService 的显式 V2 构造持有该服务，经 V2 传输适配收发，固定帧读取输出，不在 Actor 线程调用客户端监听。现有公开构造继续走旧 Server，直到业务迁移完成。
- 停止先关闭外部接纳并排空连接请求，再停止角色、地图、账号分片；不实现循环 Ask。协议 package rpg.v2，C# 命名空间 Rpg.ProtocolV2，生成到独立 src/ProtocolV2/Generated。

## Risks / Trade-offs

- 无业务的入口不可用于现有客户端登录 → 默认旧链路保持，V2 默认 Login 返回 NotImplemented，由测试内存处理器证明成功链路。
- 非线程安全的可写配置不能直接在后台修改 → V2 仅借用加载完成的 ConfigService，释放顺序为 Net/MockServer 后 Config；运行时热修改不是本次能力。
- 邮箱暂用无界队列 → 延续现有模型；真实网络接入时再定义容量与背压。

## Migration Plan

并行加入代码及协议，不迁移业务或磁盘数据。后续按独立 change 增加 V2 Handler 和协议，再统一切换。当前交付可通过本地提交整体回滚。
