# Server V2 框架

V2 在 `src/ServerV2` 独立开发，现有客户端默认仍使用旧 Server。这里的协议、账号摘要存储契约不兼容旧实现；不读取、改写或迁移旧存档。

## Actor 运行时

`Runtime` 只负责类型化 Ask/Tell、串行邮箱、INIT → RUN → STOP 与唯一分片目录。每个 Actor 是可异步等待的任务，不独占 OS 线程。首次消息触发初始化；跨 await 不进入下一回合。业务错误不会终止邮箱，初始化失败拒绝全部等待者，Tell 错误逐一通知诊断观察者。

调用方只取得 `ActorRef`。协议请求直接进入邮箱，Handler 直接返回协议回复，例如 `Ask<LoginRt>(login)`；内部请求仍可实现 `IActorRequest<TReply>` 推断回复类型。可变协议对象由单次请求独占：发送方入队后不再修改请求，Handler 返回后不再修改回复，不共享 Context 中的可写状态。内部消息使用独立或不可变数据，不传工作委托和 Actor 上下文。模块继承 `ModuleActor<TContext>`，自己创建上下文，并在创建 Actor 时冻结专用 `ActorHandlers<TContext>`。Handler 接收上下文与请求，不做文件或网络 IO；跨 Actor 协调在所属 Actor 边界进行，不形成循环 Ask。

`ActorDirectory<TKey>` 在短锁中唯一创建对象，不在创建时初始化。失败分片在后续获取时可以重建。目录关闭拒绝新创建并等待现有邮箱排空；停止前仍需由宿主先排空外部请求，避免过早关闭下游。

## 目录职责与数据流

| 目录 | 负责的事 |
| --- | --- |
| `Hosting` | MockServerService 生命周期、配置依赖与纯 .NET ServerRuntime 装配 |
| `Connections` | 连接 ID、会话代次、账号绑定、每连接串行入口和输出队列 |
| `Protocol` | 显式协议方向表、protobuf 编解码、统一错误 |
| `Routing` | 从入口或 Session 定位 Actor，直接投递协议并附带响应的会话绑定元数据 |
| `Runtime` | 通用邮箱、ActorRef、处理器契约与唯一分片目录 |
| `Modules/Login` | 按规范 username 独占账号摘要；Storage 子目录负责账号文件仓库 |
| `Modules/Role` | 按 RoleId 独占角色、物品、装备快照；Storage 子目录负责角色三表仓库 |
| `Modules/World` | 按 MapId 独占配置规格和完整对象快照；Data 保存独立模型，Storage 负责分块清单仓库 |
| `Modules/<模块>/Handler` | 所属模块的业务处理器，直接使用协议请求与回复 |
| `Modules/Common` | 模块共用的 Context 配置入口，以及 Data、Storage 子目录 |
| `Modules/Common/Storage` | 共用 JSON 编解码与单文件原子替换；不持有业务状态 |

```text
NetService --> V2 transport --> connection queue --> codec --> router
                                                        --> account Actor --> Handler
NetService <-- connection output queue <-- response codec <-- result
```

`MockServerService(EnvService, ConfigService)` 在公开构造入口校验依赖，再装配完整模块。内部注入构造只接收 `EnvService` 和非空 `ServerModules`，配置直接取自模块，消除重复传递配置造成的不一致。模块装配入口要求有效 ConfigService、账号/角色/地图三种非空仓库与登录处理器工厂；可选的角色/地图工厂及诊断回调在这里一次补齐，内部直接调用。

配置经模块装配和 Actor 必填构造参数传入 Context。LoginContext、RoleContext、WorldContext 继承 `Modules/Common/ModuleContext`，Handler 统一通过 `context.Config` 读取，例如 `context.Config.Basic.Value`、`context.Config.Item.Items`。Handler 不持有 ConfigService 字段或配置构造参数，也不访问全局服务。Config 在构造时保证非空，不再支持省略配置的测试装配；账号 Context 也必须接收完整的加载摘要。模块集成测试使用真实配置并在 Godot 宿主执行，通用邮箱和文件仓库测试仍可独立运行。

不同服务使用各自注入的配置实例；Config 属性不可替换，配置释放后读取会失败。Context、Actor 和 MockServerService 都不释放借用的配置，配置所有者应在 Net 关闭后释放。默认 `LoginHandler` 显式返回 NotImplemented；测试成功不代表认证已实现。

空请求、回复回调、保存快照在各自公开入口拒绝，避免入队或文件 IO 后才发现缺失。内部非空依赖不再逐层兜底；匿名账号、尚未启动或停止的任务、通知无回复、连接屏障无载荷、无效代次及回滚原文件缺失仍以可空值表达。JSON 根 null 的读取检查与已释放状态检查属于边界契约，继续保留。

V2 显式接入方式是 `new NetService(mockServerService, eventBus, log)`。Net 接管服务，负责序列化、排队、固定帧拉取输出及释放；传输只关闭自己绑定的连接。Env 与 Config 的所有者在 Net 释放后再释放它们。当前 `GameServices.Initialize()` 的旧构造仍运行旧业务；不增加全局 MockServer 注册。

## 身份和协议

连接 ID 由服务端生成，不进入信封。连接断开即结束 Session，重连生成新 ID。同一账号的多个连接共享账号 Actor，各自保留身份与输出；重复登录限制、断线续接暂不定义。角色状态和地图状态不放进连接 Session。

`protov2` 使用 `rpg.v2` package、`Rpg.ProtocolV2` C# 命名空间；生成文件位于 `src/ProtocolV2/Generated`，中文协议注释只修改 proto。`login.proto` 收拢登录请求、响应、角色性别枚举和摘要结构；业务协议文件按请求与响应、推送、enum 与结构的顺序组织，当前登录没有推送。每条消息使用四字段 EnvelopePb：generation、request_id、message_type、payload。请求编号非零、响应原样返回、通知为零；当前未注册业务通知。编号由客户端递增，切代次不断号，不承诺自动去重。

会话初始代次是 1。`ResetSession` 在服务端推进一代，保留账号并清空旧输出；Net 同步本连接的代次。网络适配需要以后为远程连接定义代次同步，当前只是进程内控制调用。旧代次请求和迟到结果丢弃，响应不得改用完成时最新代次。

有效信封内的未知消息、错误方向、损坏载荷和明确处理失败返回关联 ErrorRt；无法解析信封或零请求编号因不能形成有效响应关联，返回调用方错误。意外 Handler 错误另外进入服务诊断，不回传异常堆栈或请求凭据。

Login 载荷首次包含 username/password。入口对 username 使用 Trim().ToLowerInvariant()，拒绝 Windows 非法文件名及设备保留名，不做 Unicode 兼容折叠。成功后仅在代次仍有效时绑定 Session.Username；再次 Login 开始解除旧绑定，失败保持匿名。后续账号请求从 Session 取得 username，通过 RequestRouter 的账号路由进入同一分片，不重复提供账号键。

## 数据加载与写回

数据文件隔离在 `EnvService.ServerDirectory/v2`。JSON 使用 camelCase 与数字枚举：`<username>.role_summary.json` 是摘要数组；`<roleId>.role.json` 是角色对象；同角色的 `.item.json`、`.equipment.json` 是数组。摘要文件缺失得到内存空集合，不创建文件；角色三表全部必需，空表用 `[]`。不扫描或迁移旧存档，不执行字段完整性、数值、重复项、身份、关联或配置校验。JSON 无法解析、根 null 和 IO 错误使 INIT 失败，后续重新取得分片可重试。

```text
LoginActor --> LoginContext --> DataLoginSnapshot(Summaries) + saved baseline + SummariesDirty
           --> Login/Storage/IAccountRoleSummaryRepo
RoleActor  --> RoleContext  --> Role / Items / Equipments + saved baseline + DirtyTables
           --> Role/Storage/IRoleRepo
WorldActor --> WorldContext --> Layout / Objects + saved baseline + DirtyChunks
           --> World/Storage/IWorldRepo
```

快照与数据模型放在所属模块的 `Data` 子目录，Context 放在模块根目录；仓库在该模块的 `Storage` 子目录。共用文件工具在 `Modules/Common/Storage`，公共 `RoleGender` 与固定 `DataFlush` 消息在 `Modules/Common/Data`，连接数据在 `Connections/Data`。仓库通过构造注入 Actor，只读写文件，不缓存业务状态。

Login 与 Role 的 Context 统一通过 `Snapshot()` 取得不可变聚合快照，通过 `Commit(...)` 一次提交修改，参数分别为 `DataLoginSnapshot` 和 `DataRoleSnapshot`。登录快照直接包含 `Summaries : ImmutableArray<DataRoleSummary>`，Context 同样直接持有该数组，账号身份由 Actor/Context 的固定 Username 维护。`IAccountRoleSummaryRepo.LoadAsync(username)` 直接返回摘要数组，`SaveAsync(username, summaries)` 单独接收账号和数组，LoginActor 无需包装或解包。仓库入口规范化并检查账号路径；默认数组在 Context 和仓库写入入口按空数组处理，文件仍是摘要数组。Handler 可以用 `snapshot with { ... }` 构造候选数据，再提交给 Context。

Context 属性不可直接赋值，按内容与上次保存基线比较；恢复原值自动取消待写。`DirtyTables` 分别跟踪 Role、Item、Equipment，摘要使用 `SummariesDirty`。Actor 在自己的邮箱回合内 await 仓库保存，仅成功后更新保存基线；失败保留最新内存与待写标记。保存期间同一 Actor 不执行下一回合，其他分片仍可工作。

单文件同目录临时写入、刷盘后替换。角色一次修改多表时，先持久化 `<roleId>.pending.json` 原始字节回滚记录，全部替换后删除记录作为提交完成。下次读取或保存前先回滚未完成提交；恢复错误保留记录并阻止继续 IO。只从固定表名生成恢复路径，不把元数据当作路径。账号摘要与角色三表分别提交，本次不自动同步摘要，也不提供跨 Actor 事务。

`MockServerService.Tick(delta)` 由 V2 传输在 Net Tick 中推进，沿用 `EnvService.ServerFlushIntervalSeconds`：默认 5 秒、0 禁用周期、改变后重新计时。周期保存留在后台，每服务最多一个周期任务；重复到期合并，错误进入服务诊断。

`MockServerService.FlushAsync()` 捕获各连接当前队尾屏障，等待先前已接纳请求，再保存已启动分片；不启动未收消息的分片。角色组完成后保存地图组，再保存账号组，各组内并行，独立失败继续其他分片，最后汇总。各保存轮次串行衔接，失败不阻止下一次重试。V2 传输实现 `IServerWriteBackTransport`，所以 `NetService.FlushServerWrites()` 会先发送 Net 已排队消息，再同步等待服务端保存；即使周期禁用也生效。

### World 地图存储

WorldActor 的 INIT 先从借用的 `ConfigService.Map.Items` 取得条目，复制为 `DataWorldLayout(MapId, Width, Height, TileSize, ChunkSizeTiles)`，再等待 `IWorldRepo.LoadAsync(layout)` 完整加载。未知地图在仓库 IO 前失败；同地图只初始化一次，失败后重新取得分片可重试。Context 在加载成功后才发布，不重新读取配置 JSON。

`Modules/World/Data` 的 `DataWorldObject` 独立保存 `Guid ObjectId`、`Revision`、逻辑像素 `X/Y`、`MonsterTypeId`、`ItemId`、`Count`。`DataWorldSnapshot` 直接持有 `ImmutableArray<DataWorldObject>`；`DataWorldChunk` 是不可变块坐标。它们不引用旧实体或协议，不增加对象编号重复、数值范围、载荷组合和道具配置检查，也不调用旧版 ChunkAt/Validate。

`WorldContext` 固定持有 MapId、Layout，Objects 和 DirtyChunks 只能通过 `Commit(snapshot)` 改变。默认数组视为空；Commit 按 `floor(逻辑坐标 / (TileSize × ChunkSizeTiles))` 分组，将块内对象逐项按内容与保存基线比较，保留块内顺序，不使用 ImmutableArray 的引用相等。对象移出和移入的块都待写，反复修改只写最终值，恢复基线取消待写。所有对象常驻内存，当前不实现 AOI、增量加载或正式 World 业务 Handler。

默认 `RepoWorld` 接收 V2 根目录与服务诊断回调，不持有 Context 或业务缓存。存档布局为：

```text
EnvService.ServerDirectory/v2/maps/<mapId>/manifest.json
EnvService.ServerDirectory/v2/maps/<mapId>/chunks/<x>_<y>.<commitId>.json
```

JSON 使用 camelCase，当前 `formatVersion` 为 1。清单保存 `formatVersion`、`mapId`、完整 `layout`、`commitId` 和 `chunks`；各引用只有整数 `x/y` 与 GUID `commitId`。块文件保存 `formatVersion`、`mapId`、`x/y`、`commitId` 和 `objects`。文件名使用 GUID 的 N 格式，恢复时从坐标及编号重新生成路径，存档不提供任意引用路径。读取检查必需结构、版本、地图与块身份及配置布局一致性；根 null、缺失引用块、JSON 和 IO 错误使 INIT 失败。清单缺失只得到空内存地图，未修改不创建目录或文件，孤立候选不加载。

保存仅处理 DirtyChunks：非空变化块先写入新提交编号文件，经同目录临时写入和刷盘完成；未变化块复用旧引用，空块删除引用。全部候选就绪后原子替换清单作为唯一提交点。此前任何失败保留旧清单、最新内存和待写标记；WorldActor 在 DataFlush 回合直接 await 仓库，成功才接受基线，不向自身 Ask。保存期间本 Actor 的后续消息等待，其他地图仍可继续。

提交后只删除名称严格符合本仓库格式的无引用块，未知文件保留。清理失败进入诊断，诊断监听错误也不撤销已提交结果；后续成功写回会再次清理。仓库依赖同地图邮箱串行调用，不提供多进程写者协调、跨 Actor 事务或旧存档迁移。

增加功能时，先在 protov2 加协议并显式登记方向，在所属模块的 `Handler` 目录新增以协议请求与回复为类型参数的 Handler 并注册，由 Routing 直接投递。登录链路为 `Login → LoginActor → LoginHandler → LoginRt`；路由规范化账号键定位分片，原请求字段保留。Handler 只处理内存，并负责组装协议回复；Context 中的持久化 Data 模型继续独立，不引入登录请求或结果的中间 DTO。需要跨 Actor 协调时覆盖模块的 DispatchAsync，通过 ActorRef 交换数据，不把委托或上下文放进消息。加载与持久化从 Actor 初始化及宿主调度边界接入，不塞进 Handler。

关闭顺序：停止外部接纳 → 等待连接请求和已有保存轮次 → 最终保存 → 停止角色分片 → 地图分片 → 账号分片。保存失败仍完成停止并向调用方抛错，不静默吞掉失败；进程仍存活时可通过显式 Flush 修复重试。强制中断、邮箱容量与背压、真实认证、角色归属校验和跨 Actor 事务由后续目标分别确定。
