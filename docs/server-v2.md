# Server V2 框架

V2 在 `src/ServerV2` 独立开发，现有客户端默认仍使用旧 Server。这里的协议、账号摘要存储契约不兼容旧实现；不读取、改写或迁移旧存档。

## Actor 运行时

`Runtime` 只负责类型化 Ask/Tell、串行邮箱、INIT → RUN → STOP 与唯一分片目录。每个 Actor 是可异步等待的任务，不独占 OS 线程。首次消息触发初始化；跨 await 不进入下一回合。业务错误不会终止邮箱，初始化失败拒绝全部等待者，Tell 错误逐一通知诊断观察者。

调用方只取得 `ActorRef`；消息采用不可变数据记录，不传工作委托、Actor 上下文、可写集合或 protobuf 对象。模块继承 `ModuleActor<TContext>`，自己创建上下文，并在创建 Actor 时冻结专用 `ActorHandlers<TContext>`。Handler 接收上下文与请求，不做文件或网络 IO；跨 Actor 协调在所属 Actor 边界进行，不形成循环 Ask。

`ActorDirectory<TKey>` 在短锁中唯一创建对象，不在创建时初始化。失败分片在后续获取时可以重建。目录关闭拒绝新创建并等待现有邮箱排空；停止前仍需由宿主先排空外部请求，避免过早关闭下游。

## 目录职责与数据流

| 目录 | 负责的事 |
| --- | --- |
| `Hosting` | MockServerService 生命周期、配置依赖与纯 .NET ServerRuntime 装配 |
| `Connections` | 连接 ID、会话代次、账号绑定、每连接串行入口和输出队列 |
| `Protocol` | 显式协议方向表、protobuf 编解码、统一错误 |
| `Routing` | 从入口或 Session 定位 Actor，转换独立内部消息和响应 |
| `Runtime` | 通用邮箱、ActorRef、处理器契约与唯一分片目录 |
| `Modules/Login` | 按规范 username 独占该账号的全部角色摘要 |
| `Modules/Role` | 按正整数全局 RoleId 的独占状态边界 |
| `Modules/World` | 按正整数 MapId 的独占状态边界 |
| `Storage` | 账号摘要数据与 IO 接口契约，不实现文件读写 |

```text
NetService --> V2 transport --> connection queue --> codec --> router
                                                        --> account Actor --> Handler
NetService <-- connection output queue <-- response codec <-- result
```

`MockServerService(ConfigService)` 借用配置并拥有 Runtime。后续业务通过构造注入同一个 `ConfigService`，不访问 `GameServices`。默认 `LoginHandler` 显式返回 NotImplemented；测试成功不代表认证已实现。

V2 显式接入方式是 `new NetService(mockServerService, eventBus, log)`。Net 接管服务，负责序列化、排队、固定帧拉取输出及释放；传输只关闭自己绑定的连接。Config 的所有者在 Net 释放后再释放配置。当前 `GameServices.Initialize()` 的旧构造仍运行旧业务；不增加全局 MockServer 注册。

## 身份和协议

连接 ID 由服务端生成，不进入信封。连接断开即结束 Session，重连生成新 ID。同一账号的多个连接共享账号 Actor，各自保留身份与输出；重复登录限制、断线续接暂不定义。角色状态和地图状态不放进连接 Session。

`protov2` 使用 `rpg.v2` package、`Rpg.ProtocolV2` C# 命名空间；生成文件位于 `src/ProtocolV2/Generated`，中文协议注释只修改 proto。每条消息使用四字段 EnvelopePb：generation、request_id、message_type、payload。请求编号非零、响应原样返回、通知为零；当前未注册业务通知。编号由客户端递增，切代次不断号，不承诺自动去重。

会话初始代次是 1。`ResetSession` 在服务端推进一代，保留账号并清空旧输出；Net 同步本连接的代次。网络适配需要以后为远程连接定义代次同步，当前只是进程内控制调用。旧代次请求和迟到结果丢弃，响应不得改用完成时最新代次。

有效信封内的未知消息、错误方向、损坏载荷和明确处理失败返回关联 ErrorRt；无法解析信封或零请求编号因不能形成有效响应关联，返回调用方错误。意外 Handler 错误另外进入服务诊断，不回传异常堆栈或请求凭据。

Login 载荷首次包含 username/password。入口对 username 使用 Trim().ToLowerInvariant()，拒绝 Windows 非法文件名及设备保留名，不做 Unicode 兼容折叠。成功后仅在代次仍有效时绑定 Session.Username；再次 Login 开始解除旧绑定，失败保持匿名。后续账号请求从 Session 取得 username，通过 RequestRouter 的账号路由进入同一分片，不重复提供账号键。

## 摘要存储与后续扩展

`DataAccountRoleSummaries` 包含规范账号和不可变角色摘要集合；`IAccountRoleSummaryRepo` 以账号为加载、保存边界，约定文件名是 `username.role_summary.json`。没有真实仓库实现，不扫描旧 `<RoleId>.role_summary.json`。摘要数据与生成协议相互独立，不共享可写 protobuf 对象。

增加功能时，先在 protov2 加协议并显式登记方向，在 Routing 转换成不可变内部消息，再在所属模块注册该 Actor 专用 Handler。Handler 只处理内存；需要跨 Actor 协调时覆盖模块的 DispatchAsync，通过 ActorRef 交换数据，不把委托或上下文放进消息。加载与持久化从 Actor 初始化及宿主调度边界接入，不塞进 Handler。

关闭顺序：停止外部接纳 → 排空所有连接已接纳请求 → 停止角色分片 → 地图分片 → 账号分片。强制中断、邮箱容量与背压、真实认证、角色归属校验和真实磁盘事务由后续目标分别确定。
