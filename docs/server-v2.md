# Server V2 框架

V2 在 `src/ServerV2` 独立开发，默认客户端使用 V2 TCP；旧 Server 由测试显式装配。这里的协议、账号摘要存储契约不兼容旧实现；不读取、改写或迁移旧存档。

## Actor 运行时

`Runtime` 只负责类型化 Ask/Tell、串行邮箱、INIT → RUN → STOP 与唯一分片目录。每个 Actor 是可异步等待的任务，不独占 OS 线程。首次消息触发初始化；跨 await 不进入下一回合。业务错误不会终止邮箱，初始化失败拒绝全部等待者，Tell 错误逐一通知诊断观察者。

调用方只取得 `ActorRef`。协议请求直接进入邮箱，Handler 直接返回协议回复，例如 `Ask<LoginRt>(login)`；非 proto 的运行时请求仍可实现 `IActorRequest<TReply>` 推断回复类型。可变协议对象由单次请求独占：发送方入队后不再修改请求，接收 Handler 返回后不留存并修改回复，不共享 Context 中的可写状态。内部消息不传工作委托、Repo 或 Actor 上下文。模块继承 `ModuleActor<TContext>`，自己创建上下文，并在创建 Actor 时冻结专用 `ActorHandlers<TContext>`。Handler 接收上下文与请求，不做文件或网络 IO；跨 Actor 协调在所属模块的 Async Handler 中通过 Context.Actors 进行。跨 await 仍占源 Actor 同一串行回合；禁止同 Actor 自身 Ask 和循环等待，当前不提供死锁检测。

`ActorDirectory<TKey>` 在短锁中唯一创建对象，不在创建时初始化。失败分片在后续获取时可以重建。目录关闭拒绝新创建并等待现有邮箱排空；停止前仍需由宿主先排空外部请求，避免过早关闭下游。

## 目录职责与数据流

| 目录 | 负责的事 |
| --- | --- |
| `Hosting` | 可空 MockServerService 所有权、独立 MockServer、TCP 监听与 ServerRuntime 装配 |
| `Connections` | 连接 ID、会话代次、账号与当前角色绑定、每连接串行入口和输出队列 |
| `Protocol` | 显式协议方向表、protobuf 编解码、统一错误 |
| `ActorProtocol/Generated` | 由 protov2/actor 生成的内部消息，不进入客户端编解码注册表 |
| `Routing` | 从入口或 Session 定位 Actor，直接投递协议并附带响应的会话绑定元数据 |
| `Runtime` | 通用邮箱、ActorRef、处理器契约与唯一分片目录 |
| `Modules/Login` | 按规范 username 独占账号摘要；Storage 子目录负责账号文件仓库 |
| `Modules/Role` | 按 RoleId 独占 RoleCache 及三表数据缓存；Storage 子目录负责角色三表仓库 |
| `Modules/World` | 按 MapId 独占 WorldCache、配置规格和对象缓存；Data 保存独立模型，Storage 负责分块清单仓库 |
| `Modules/<模块>/Handler` | 所属模块的业务处理器，直接使用协议请求与回复 |
| `Modules/<模块>/Async` | 与 Handler 平级的跨 Actor 业务步骤，共用 IActorHandler/ActorHandlers 注册分发 |
| `Modules/Common` | ModuleContext 的 Config/Actors 借用入口及 IActorLocator，以及 Cache、Data、Storage 子目录 |
| `Modules/Common/Storage` | 共用 JSON 编解码与单文件原子替换；不持有业务状态 |

```text
NetService --> V2 transport --> connection queue --> codec --> router
                                                        --> account Actor --> Handler
NetService <-- connection output queue <-- response codec <-- result
```

`MockServerService(EnvService, ConfigService)` 仅校验并保存依赖，Server 初始为空。服务器模式登录时创建普通 MockServer，监听成功后交付持有；纯客户端不创建实例。内部注入构造只接收 `EnvService` 和非空 `ServerModules`，配置直接取自模块，消除重复传递配置造成的不一致。模块装配入口要求有效 ConfigService、账号/角色/地图三种非空仓库与登录处理器工厂；可选的角色/地图工厂及诊断回调在这里一次补齐，内部直接调用。

配置经模块装配和 Actor 必填构造参数传入 Context。LoginContext、RoleContext、WorldContext 继承 `Modules/Common/ModuleContext`，Handler 统一通过 `context.Config` 读取，例如 `context.Config.Basic.Value`、`context.Config.Item.Items`。Handler 不持有 ConfigService 字段或配置构造参数，也不访问全局服务。Config 在构造时保证非空，不再支持省略配置的测试装配；账号 Context 也必须接收完整的加载摘要。模块集成测试使用真实配置并在 Godot 宿主执行，通用邮箱和文件仓库测试仍可独立运行。

不同服务使用各自注入的配置实例；Config 属性不可替换，配置释放后读取会失败。Context、Actor 和 MockServerService 都不释放借用的配置，配置所有者应在 Net 关闭后释放。默认 LoginHandler 验证演示凭据 demo/demo，规范账号键参与匹配、密码原样匹配；正式认证尚未实现。创建和删除摘要归同一 LoginActor，成功回复完整可见列表，失败使用 ErrorRt。

`ServerModules` 拥有目录、身份定位和生命周期，并实现窄 `IActorLocator`：`Login(string)`、`Role(long)`、`World(int)` 仅返回 `ActorRef`。三个 Actor 和 Context 均由构造必需注入此依赖，Context 通过只读 `Actors` 借用，不负责释放，不使用全局 locator 或可空回填。只定位分片不触发 INIT。不同服务的 Context 只定位本服务目录；目录工厂在 Get 时才执行，因此装配中可以捕获所属 ServerModules。

空请求、回复回调、保存快照在各自公开入口拒绝，避免入队或文件 IO 后才发现缺失。内部非空依赖不再逐层兜底；匿名账号、尚未启动或停止的任务、通知无回复、连接屏障无载荷、无效代次及回滚原文件缺失仍以可空值表达。JSON 根 null 的读取检查与已释放状态检查属于边界契约，继续保留。

V2 显式接入方式是 `new NetService(mockServerService, eventBus, log)`。Net 接管服务，负责序列化、排队、固定帧拉取输出及释放；传输只关闭自己绑定的连接。Env 与 Config 的所有者在 Net 释放后再释放它们。当前 `GameServices.Initialize()` 默认使用 V2 TCP，Net 构造仅创建空的服务器持有服务；显式进程内构造只服务隔离测试，不增加全局 MockServer 注册。

## 身份和协议

连接 ID 由服务端生成，不进入信封。连接断开即结束 Session，重连生成新 ID。同一账号的多个连接共享账号 Actor，各自保留身份与输出；重复登录限制、断线续接暂不定义。角色状态和地图状态不放进连接 Session。

外部 `protov2` 使用 `rpg.v2` package、`Rpg.ProtocolV2` C# 命名空间；生成文件位于 `src/ProtocolV2/Generated`，中文协议注释只修改 proto。`login.proto` 收拢账号请求及响应，导入 `role.proto` 的性别枚举与摘要；`role.proto` 还保存公开状态和本人私有角色结构，`bag.proto` 保存完整私有物品，`map.proto` 保存初始范围，`world.proto` 保存 EnterWorld/EnterWorldRt；业务协议文件按请求与响应、推送、enum 与结构的顺序组织，当前账号业务没有推送。每条外部消息使用四字段 EnvelopePb：generation、request_id、message_type、payload。请求编号非零、响应原样返回、通知为零；当前未注册业务通知。编号由客户端递增，切代次不断号，不承诺自动去重。

内部源放 `protov2/actor`，按接收方模块分文件，当前只有 `world.proto` 和 `role.proto`，不预建空 login/common 文件。全部使用 `syntax = "proto3"`、`package rpg.v2.actor`、`csharp_namespace = "Rpg.ServerV2.ActorProtocol"`，允许导入根目录的角色、背包与地图共享结构。请求使用动作名、响应追加 `Rt`、结构与枚举使用 `Pb`；文件顺序为请求响应→推送→枚举结构，无内容部分省略。生成文件在 `src/ServerV2/ActorProtocol/Generated`；rpg.csproj 的外部 Protobuf 通配排除 protov2/actor/**/*.proto，内部 ProtoRoot 为 `protov2`，内部逻辑文件路径为 `actor/role.proto`、`actor/world.proto`，共享结构仅生成编译一次、GrpcServices 为 None；生成 C# 排除默认 Compile 通配，由 Grpc.Tools 纳入编译。生成 C# 与 Godot UID 一并提交，不手改生成文件。

第一版进程内直接移交 proto，不经客户端信封或额外序列化，不增加中间 C# 请求结果包装或逐请求接口绑定。完整类型名如 `rpg.v2.actor.GetWorldObjectCount`；MessageCodec 显式登记外部 Login/CreateRole/DeleteRole/EnterWorld 及对应回复和 ErrorRt，内部消息由客户端投递时返回 UnknownMessage。持久化继续使用独立 Data，不用 proto 落盘。

最小只读链路为 `GetRoleWorldObjectCount → Role/Async/GetRoleWorldObjectCountHandler → World/Handler/GetWorldObjectCountHandler`。Role 从当前 `Role.MapId` 定位，World 返回 `Objects.Length`，再由 Role 返回 `GetRoleWorldObjectCountRt.Count`：

```csharp
var reply = await context.Actors.World(context.Cache.Role.Value.MapId)
    .Ask<GetWorldObjectCountRt>(new GetWorldObjectCount());
```

目标初始化与处理错误传播到源调用者。默认 Role/World 注册对应处理器；显式提供注册工厂时保持调用者控制，不隐式追加示例。此查询不修改存档或保存基线，不扩展通知框架。

会话初始代次是 1。`ResetSession` 在服务端推进一代，保留账号和当前角色绑定并清空旧输出；Net 同步本连接的代次。TCP 请求携带更高代次时直接采用，允许跳号；不增加握手业务消息。旧代次请求和迟到结果丢弃，响应不得改用完成时最新代次。

有效信封内的未知消息、错误方向、损坏载荷和明确处理失败返回关联 ErrorRt；无法解析信封或零请求编号因不能形成有效响应关联，返回调用方错误。意外 Handler 错误另外进入服务诊断，不回传异常堆栈或请求凭据。

Login 载荷首次包含 username/password。入口对 username 使用 Trim().ToLowerInvariant()，拒绝 Windows 非法文件名及设备保留名，不做 Unicode 兼容折叠。成功后仅在代次仍有效时绑定 Session.Username；再次 Login 开始解除账号和角色绑定，失败保持匿名。后续账号请求从 Session 取得 username，通过 RequestRouter 的账号路由进入同一分片，不重复提供账号键。

## 数据加载与写回

数据文件隔离在 `EnvService.ServerDirectory/v2`。JSON 使用 camelCase 与数字枚举：`<username>.role_summary.json` 是摘要数组；`<roleId>.role.json` 是角色对象；同角色的 `.item.json`、`.equipment.json` 是数组。摘要文件缺失得到内存空集合，不创建文件；角色三表全部不存在时建立尚未建档工作区，部分缺失拒绝 INIT；已有空表用 `[]`。不扫描或迁移旧存档，存储层不执行字段完整性、数值、重复项、身份、关联或配置校验；Login 的 DataCacheRoleSummary 校验新建名称、外观和删除归属。JSON 无法解析、根 null 和 IO 错误使 INIT 失败，后续重新取得分片可重试。

```text
LoginContext --> LoginCache --> DataCacheRoleSummary --> 摘要数组及墓碑
RoleContext  --> RoleCache  --> DataCacheRole / DataCacheItem / DataCacheEquipment --> 三表数据
WorldContext --> WorldCache --> DataCacheWorldObject --> 动态对象数组
Actor --> 模块 Cache.Snapshot() --> Repo --> 成功后 Cache.AcceptSaved()
```

Context 只引用所属模块 Cache，并继承 `ModuleContext` 的 Config/Actors 借用入口，不展开数据或转发 Commit。模块根目录保留 Context，`Cache` 子目录放可变工作区及各类 DataCache，`Data` 放不可变数据和仓库导出快照，`Storage` 放仓库。共性 `DataCache<T>`、`DataCacheArray<T>` 在 `Modules/Common/Cache` 管理当前值、成功保存基线、提交及内容比较；共用文件工具在 `Modules/Common/Storage`，公共性别与保存消息在 `Modules/Common/Data`。

模块 Cache 组织子缓存和固定身份，提供 `Snapshot()/Commit(...)` 聚合导出及提交。Snapshot 仅是不可变传输值，不是运行时工作区。Handler 可以通过 `context.Cache.Role.Commit(...)` 修改单表，也可以构造 `DataRoleSnapshot` 一次提交三表；数组默认值按空数组处理。DataCache 的 Value 只读，只能通过 Commit 替换；直接修改子缓存立即反映模块的 DirtyTables、SummariesDirty 或 DirtyChunks，恢复基线取消待写。摘要创建、删除、可见排序和嵌套装备内容比较归 `DataCacheRoleSummary`；Handler 负责读取协议、调用缓存并映射回复。

仓库构造注入 Actor，只读写文件，不持有业务状态。账号仓库直接加载/保存摘要数组，账号键独立传入。Actor 在自己的邮箱回合内 await 仓库保存，仅成功后通知 Cache 接受各 DataCache 的当前值为新基线；失败保留内存及待写状态。同一 Actor 保存期间不执行下一回合，其他分片仍可工作。不可变导出不会因工作区后续提交而改变，不跨 Actor 共享可变 Cache。

单文件同目录临时写入、刷盘后替换。角色一次修改多表时，先持久化 `<roleId>.pending.json` 原始字节回滚记录，全部替换后删除记录作为提交完成。下次读取或保存前先回滚未完成提交；恢复错误保留记录并阻止继续 IO。只从固定表名生成恢复路径，不把元数据当作路径。账号摘要与角色三表分别提交，仓库不自动同步摘要，也不提供跨 Actor 事务；进入业务显式根据实际角色同步摘要。

监听成功后由 MockServer 的独立后台时钟推进保存检测；无监听的进程内测试可手动 Tick，沿用 `EnvService.ServerFlushIntervalSeconds`：默认 5 秒、0 禁用周期、改变后重新计时。周期保存留在后台，每服务最多一个周期任务；重复到期合并，错误进入服务诊断。

`MockServerService.FlushAsync()` 捕获各连接当前队尾屏障，等待先前已接纳请求，再保存已启动分片；不启动未收消息的分片。角色组完成后保存地图组，再保存账号组，各组内并行，独立失败继续其他分片，最后汇总。各保存轮次串行衔接，失败不阻止下一次重试。V2 传输实现 `IServerWriteBackTransport`，所以 `NetService.FlushServerWrites()` 会先发送 Net 已排队消息，再同步等待服务端保存；即使周期禁用也生效。

### World 地图存储

WorldActor 的 INIT 先从借用的 `ConfigService.Map.Items` 取得条目，复制为 `DataWorldLayout(MapId, Width, Height, TileSize, ChunkSizeTiles)`，再等待 `IWorldRepo.LoadAsync(layout)` 完整加载。未知地图在仓库 IO 前失败；同地图只初始化一次，失败后重新取得分片可重试。Context 在加载成功后才发布，不重新读取配置 JSON。

`Modules/World/Data` 的 `DataWorldObject` 独立保存 `Guid ObjectId`、`Revision`、逻辑像素 `X/Y`、`MonsterTypeId`、`ItemId`、`Count`。`DataWorldSnapshot` 直接持有 `ImmutableArray<DataWorldObject>`；`DataWorldChunk` 是不可变块坐标。它们不引用旧实体或协议，不增加对象编号重复、数值范围、载荷组合和道具配置检查，也不调用旧版 ChunkAt/Validate。

`WorldContext.Cache` 引用 `WorldCache`，后者固定持有 MapId、Layout，Objects 引用 `DataCacheWorldObject`。对象缓存 Commit 替换完整数组，DirtyChunks 由该缓存比较产生。默认数组视为空；比较按 `floor(逻辑坐标 / (TileSize × ChunkSizeTiles))` 分组，将块内对象逐项按内容与保存基线比较，保留块内顺序，不使用 ImmutableArray 的引用相等。对象移出和移入的块都待写，反复修改只写最终值，恢复基线取消待写。所有对象常驻内存，提供内部对象数量查询和只读进入的初始九块快照，不实现 AOI 增量或正式 World 玩法业务。

默认 `RepoWorld` 接收 V2 根目录与服务诊断回调，不持有 Context 或业务缓存。存档布局为：

```text
EnvService.ServerDirectory/v2/maps/<mapId>/manifest.json
EnvService.ServerDirectory/v2/maps/<mapId>/chunks/<x>_<y>.<commitId>.json
```

JSON 使用 camelCase，当前 `formatVersion` 为 1。清单保存 `formatVersion`、`mapId`、完整 `layout`、`commitId` 和 `chunks`；各引用只有整数 `x/y` 与 GUID `commitId`。块文件保存 `formatVersion`、`mapId`、`x/y`、`commitId` 和 `objects`。文件名使用 GUID 的 N 格式，恢复时从坐标及编号重新生成路径，存档不提供任意引用路径。读取检查必需结构、版本、地图与块身份及配置布局一致性；根 null、缺失引用块、JSON 和 IO 错误使 INIT 失败。清单缺失只得到空内存地图，未修改不创建目录或文件，孤立候选不加载。

保存仅处理 DirtyChunks：非空变化块先写入新提交编号文件，经同目录临时写入和刷盘完成；未变化块复用旧引用，空块删除引用。全部候选就绪后原子替换清单作为唯一提交点。此前任何失败保留旧清单、最新内存和待写标记；WorldActor 在 DataFlush 回合直接 await 仓库，成功才接受基线，不向自身 Ask。保存期间本 Actor 的后续消息等待，其他地图仍可继续。

提交后只删除名称严格符合本仓库格式的无引用块，未知文件保留。清理失败进入诊断，诊断监听错误也不撤销已提交结果；后续成功写回会再次清理。仓库依赖同地图邮箱串行调用，不提供多进程写者协调、跨 Actor 事务或旧存档迁移。

增加外部功能时，先在 protov2 加协议并显式登记方向，在所属模块的 `Handler` 目录新增以协议请求与回复为类型参数的 Handler 并注册，由 Routing 直接投递。登录链路为 `Login → LoginActor → LoginHandler → LoginRt`；路由规范化账号键定位分片，原请求字段保留。Handler 只处理内存，并负责组装协议回复；DataCache 持有的持久化 Data 模型继续独立，不引入登录请求或结果的中间 DTO。内部协议放 protov2/actor，不注册客户端方向表。需要跨 Actor 协调时在所属模块 `Async` 目录实现 Handler，通过 Context.Actors 定位并使用现有 ActorRef Ask/Tell 邮箱交换数据，不把委托或上下文放进消息。DispatchAsync 保留保存等基础设施边界，不承载业务编排。加载与持久化从 Actor 初始化及宿主调度边界接入，不塞进 Handler。

关闭顺序：停止外部接纳 → 等待连接请求和已有保存轮次 → 最终保存 → 停止角色分片 → 地图分片 → 账号分片。保存失败仍完成停止并向调用方抛错，不静默吞掉失败；进程仍存活时可通过显式 Flush 修复重试。强制中断、邮箱容量与背压、真实认证及跨 Actor 事务由后续目标分别确定；进入的账号归属检查已经实现。


## 登录与角色列表迁移

默认客户端继续走旧 Server。独立手工入口：

```powershell
godot-mono --path . res://tests/server_v2_role_management.tscn
```

使用 demo/demo 登录；username 沿用 Trim().ToLowerInvariant()，密码原样匹配。LoginRt、CreateRoleRt、DeleteRoleRt 都只返回按 role_id 升序的完整可见 roles，不含 success/error。统一 ErrorRt 新增 InvalidCredentials、RoleNotFound、RoleNameConflict；非法输入沿用 InvalidRequest。创建和删除从连接 Session 取得账号，不接受载荷账号键。

创建由 LoginCache.Summaries.Create(...) 校验并一次提交 summary，删除由其 Delete(...) 标记墓碑：名称去首尾空白后为 1–16 个 UTF-16 字符、无控制字符，当前未删除名称忽略大小写唯一；性别为男女，服务端头型 1–74，页面为 9–72；初始等级 1，装备为空。DataRoleSummary 的 IsDeleted 默认为 false，删除不物理移除摘要。编号从包含墓碑的最大值加一，空集合从 1003 开始，溢出拒绝；此规则仅适用当前单演示账号，多账号编号分配待定。

创建、删除不启动 RoleActor/WorldActor，不产生角色三表或地图文件。摘要沿用周期、显式 Flush、关闭补写和失败重试；成功响应表示内存提交，强杀可能丢失未落地变更。进入世界先检查未删除账号归属，再加载或首次创建三表；不读取旧存档。

独立场景通过测试工厂注册完整服务组，Net 拥有 V2 服务，Session 和 LoginCache 按显式 V2 模式使用新协议，不转译旧消息。每个客户端仅一个账号或进入操作在途；缓存完整验证并原子替换后才通知页面，失败保留列表与命名草稿并解除等待，返回登录切换代次。角色页面复用现有预览，进入按钮启用，世界仅供只读展示。自动验收入口为 tests/server_v2_role_management_smoke.tscn，tools/check.ps1 -Tests 会运行真实鼠标按钮流程。

## 进入世界与初始展示

独立场景仍为 `tests/server_v2_role_management.tscn`。默认 F5 的 `scenes/main.tscn` 保持旧 Server。手动流程：demo/demo 登录 → 创建角色并预览 → 进入游戏 → 查看地图、人物、生命魔力与角色窗口、只读背包装备 → 系统菜单返回角色选择 → 切角或再次进入 → 关闭后重新运行场景恢复。移动、物品变更、拾取及聊天 GM 已禁用；本地窗口、图鉴、HUD 与设置可用。

外部新增的唯一操作是 `EnterWorld(role_id)`，回复 `EnterWorldRt(state, role, bag, map)`。公开 state 根节点只保存一份角色编号，profile/status/position/equipment 分别表示公开资料、生命魔力、权威地图逻辑位置和装备配置编号；role 保存经验及背包扩容，bag 包括背包和已穿戴的私有实例。map 仅含完整初始范围，不预加增量、订阅序号、重同步或移动确认字段。对象自身 revision 沿用存储含义，object_id 使用 GUID N 格式。新增文件不使用 `_data` 后缀。

消息流固定 `连接会话 → LoginActor → PrepareRole → RoleActor → GetWorldEntrySnapshot → WorldActor`，响应反向返回。Login 在任何角色定位前检查正数编号及未删除摘要归属，并先保存待写账号摘要，防止摘要丢失造成编号复用。Role INIT 完整加载已有三表；三表全部不存在时只建立尚未建档的 RoleCache，普通查询、定位和 Flush 不创建数据。只有可信 PrepareRole 根据摘要与配置建立候选：生命100、魔力60、经验0、升级经验500、扩容0、物品装备为空、NextInstanceId为1；初始地图取配置，男性(16016,16016)、女性(16144,16080)，按地图边缘裁剪。初始空表也全部待写，保存成功才接受基线；失败保留候选和脏状态，修复后可显式 Flush 或重新进入重试。

已有角色不重置生命、位置或物品。部分缺表、损坏和 null 拒绝初始化，修复文件后重新请求可重建分片；已加载且可解析但业务字段非法的数据在进入投影时拒绝，不覆盖源数据，修复文件并重启后重新加载。全部三表丢失后会按首次进入重建，不增设初始化标记。地图由配置规格返回中心及周围八块，按边缘裁剪、包含空块；空地图只读进入不生成地图文件。地图失败时已完成的角色建档保留供修复重试。

Login 根据准备后的实际公开状态同步摘要，本次回复的客户端缓存也立即同步自己的摘要。调用链没有 Role → Login 的反向 Ask。后续装备等操作须在该业务内同步受影响数据，不依赖返回选角修正。

响应完成时仅在原代次仍有效的短锁内绑定或替换 Session.RoleId；失败保留原绑定，再次认证或断开清除绑定。代次重置只隔离旧结果，不产生退出或离线事件；不同连接可同时只读进入同角色，不建立独占锁或在线成员。

Session 的 WorldEntryCache 在 Cache 阶段直接将 V2 转成现有不可变展示数据，不制造旧协议对象。完整校验角色身份、物品与公开装备、地图规格/九块/对象及摘要候选后才连续提交；普通 UI 监听看到完整缓存。失败保留已有列表及已接纳世界并解除等待，返回登录释放缓存且切换代次。

返回选角、返回登录、菜单退出和 V2 系统关闭共用现有本地写回能力，失败时保留页面并显示错误供重试。成功后清理客户端世界、角色及地图，隔离旧结果；不发送 LeaveWorld、网络保存或退出补同步消息，不停止共享 Actor。真实传输没有本地写回能力时不发送保存消息，服务端自行周期、失败重试和关闭补写。强杀允许丢失未落盘变更。

自动验收 `tests/server_v2_world_entry_smoke.tscn` 通过真实鼠标覆盖进入、地图人物HUD、只读背包装备、返回、切角、重启及保存失败后页面/系统关闭保留与修复重试，已纳入 `tools/check.ps1 -Tests`。角色管理烟测还覆盖尚未进入世界时，返回登录及系统关闭也执行本地写回，失败保留选角页并显示错误、修复后重新返回。字节与文件测试覆盖归属先于建档、部分缺失/损坏、首次保存失败重试、已有实例/地图对象不改写、九块边缘及迟到绑定；客户端测试覆盖跨领域原子校验、只读写入口和旧代次隔离。

## TCP 托管与登录配置

默认登录可选服务器模式（端口）或纯客户端（字面 IP 和端口），初始 7777 / 127.0.0.1。服务器模式的本机玩家也通过真实 TCP；返回登录或认证失败不关闭监听，相同配置重试复用服务器，修改模式或监听端口须确认后关闭原服务器。应用退出排空并保存。

TCP 为四字节大端长度加 EnvelopePb，单包 4 MiB；每连接输出最多 64 包或 8 MiB，全实例 64 MiB。后台 IO 只排队，固定帧分发事件。连接五秒、请求十五秒超时，失败后手动重试。本地显式写回先等待后台收到此前请求响应再 Flush，纯客户端没有此能力。业务协议不增加连接配置或会话隔离字段。
