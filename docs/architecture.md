# 客户端架构

## 正式 Server 框架

业务处理器每类只实现一个 `IActorHandler`，类型与文件按请求消息名加 `Handler` 命名。跨 Actor 编排放在所属模块 `Async/`，本地处理放在 `Handler/`，这两个目录只放 Handler。跨 Actor 共用逻辑放在 `Modules/Common/Utils/`，Actor 内共用逻辑放在 `Modules/Xxx/Utils/`；处理器通过工具类复用规则与投影，不相互调用辅助方法。

`ServerModules` 在构造方法内统一声明 Login、Role、World 的完整处理器注册，每个分片创建独立表，不接受外部处理器工厂。`MockServer` 只传入配置、存储、诊断和共同保存依赖。宿主通过内部 `IServerModules` 借用完整模块；受控 Handler、阻塞与故障实验由测试侧 `DoubleServerModules` 装配，正式业务验收仍直接使用 `ServerModules`。

默认 `scenes/main.tscn` 使用 `src/Server` 与 `proto`。GameServices 创建并持有空 `MockServerService`，通过只读 `MockServer` 入口提供访问，再注入 Net，登录后才按模式创建独立 `MockServer` 或连接远端。服务器拥有后台 TCP、20Hz单调时钟推进、10Hz批量AOI和保存关闭流程，本机玩家与远端玩家走相同字节链路。账号admin/demo1/demo2密码123；本机服务器模式仅允许admin。角色编号通过与账号无关的持久化递增序列分配，从 1003 开始。

运行时按账号、角色、地图分片并保持INIT/RUN/STOP和串行邮箱；Context只引用所属Cache，Cache组织不可变DataCache值，单分片 Storage 在 Actor 边界做 IO；正式保存由邮箱捕获不可变快照，专用物品批次在邮箱外写入后回传捕获基线。跨Actor调用固定Login→Role→World，由Async Handler编排，World不向Role Ask。业务proto直接进入Handler，连接/认证/控制版本只在 `DataActorCall` 邮箱元数据和运行时目录中校验，不增加业务隔离字段；批量发布保留观察者到原身份的不可变映射，输出不重新套用最新绑定。

模块持久化接口统一为 `ILoginStorage`、`IRoleStorage`、`IWorldStorage`，文件实现分别为 `LoginStorage`、`RoleStorage`、`WorldStorage`，位于所属模块的 `Storage/`。Storage 负责加载、保存、提交恢复及登录模块的角色编号预留，不持有业务状态；`LoginCache`、`RoleCache`、`WorldCache` 管理内存数据、修改及保存基线，Actor 协调两者。

Role将权威坐标提交角色基础表，目标、速度、控制和确认均为不落盘的临时缓存。World保存不可变公开角色和块索引，首次可见发完整RoleWorldStatePb，持续移动只发完整位置组。返回选角通过LeaveWorld解除该连接的角色控制、停止移动并移除在线成员，其他玩家在下一次AOI发布看见角色消失；账号认证与TCP连接保留。断线、重新认证和成功切角也解绑，同角色由新连接接管并踢回旧连接的登录页。独立调度只推进移动/控制变化/投影重试角色，保存关闭包含已接纳轮次。

客户端由WorldSyncCache完整验证世界帧后原子接纳自身、地图和公开远端角色；序号缺口只请求一次全量。WorldService立即预测并以20Hz合并最新目标，确认后保留较新意图；其他玩家用100ms缓冲及最多100ms外推，经Action更新ECS，MainController复用ActorView呈现。默认开放移动、开发期 GM、背包整理和主副手穿脱；地面丢弃、销毁与走近拾取也已开放。物品请求按最新状态执行，不带预期版本；本人完整物品与装备经 RoleItemsCache 验证后原子接纳，World 按内容发送位置和公开装备增量。物品操作与保存共用单实例信号量，在进入邮箱前获取；已执行操作完成摘要收尾，旧身份结果仍禁止投递。地面转移直接经 Role→World 创建、查询与删除；拾取请求立即回复，时钟在到达后经 Login→Role 完成并通知 success，明确取消静默。角色三表、摘要和地图清单共用单批次回滚记录，启动先恢复，旧地图块延后清理；保存期间的新移动保持待写。详细协议和运行方式见[Server框架](server.md)。

正式协议和服务器只有一套，测试使用相同模块；原双协议装配已移除。存档直接位于 `user://server`，首次启动提升已有 `server/v2` 内容，不覆盖目标文件。

## 服务、数据和生命周期

客户端共享业务枚举放在 `src/Const/`，使用 `Rpg.Const` 命名空间，例如 `RoleGender` 和 `RoleMotion`。

`IService` 位于 `src/Services/`，统一约定释放与只读 `IsDisposed` 状态。十一个服务继承薄基类 `Service`，共用释放状态和受保护的 `ThrowIfDisposed()`；具体 `Dispose()` 仍由各服务实现，保持自己的标记时机、资源清理和异常处理，不增加释放钩子或统一清理模板。缓存、传输与注册句柄保留自身生命周期。`GameServices` 是主线程的固定服务集合，提供只读 `Env`、`Log`、`Config`、`Setting`、`EventBus`、`MockServer`、`Net`、`Session`、`World`、`Animation`、`Items` 入口。参考 Dalamud 的服务契约、构造依赖与逆依赖卸载原则；当前服务数量固定，通过显式组合表达依赖。服务之间只能借用构造参数传入的同次启动实例，禁止在任何服务及其内部类型中访问 GameServices，禁止循环依赖、属性回填、延迟解析与工厂委托绕过构造顺序；依赖字段保持只读。Service 构造参数与依赖字段使用非空类型，构造入口拒绝空值和已释放依赖；只有尚未启动的普通 MockServer 实例保持可空。Godot 节点通过全局入口调用业务，不缓存服务、账号缓存或世界管理器。节点只拥有 Godot 节点、订阅句柄、界面布局和上次呈现的不可变快照。

| 服务 | 拥有的状态和行为 | 内部依赖 |
| --- | --- | --- |
| `EnvService Env` | 统一持有配置、Schema、设置与模拟服务端目录，以及 .env 日志、锚点显示和移动速度 | 无依赖根服务 |
| `LogService Log` | 通信与成功文件提交的控制台日志，仅指定消息标记着色 | 构造绑定 Env；使用 Godot PrintRich |
| `ConfigService Config` | 构造时加载、校验并持有配置 | Env；具体 CfgCache 统一读取 JSON 与 Schema |
| `SettingService Setting` | 构造时加载系统设置，内部持有角色设置并修改、保存 | Env、Log；私有 JSON 读写方法 |
| `EventBusService EventBus` | 唯一类型事件域、缓存优先分发、监听错误与订阅生命周期 | 内部监听集合与重入队列 |
| `MockServerService MockServer` | 持有登录时才创建的本机 MockServer，负责启动、保存和关闭 | Env、Config；由完整注册统一释放 |
| `NetService Net` | protobuf 请求队列、固定帧收发及解码后发布消息 | MockServerService、EventBus、Log；内部 `INetTransport` 只借用服务器服务并管理自己的连接 |
| `SessionService Session` | 持有登录尝试的摘要与角色缓存，提交登录、选角和角色管理请求并提供快照 | `NetService`、内部 `LoginCache`、`RoleCache` 与 `MapCache` |
| `WorldService World` | 地图、ECS、输入转换、角色初始化与权威同步 | Env、Config、Setting、Session；内部只读地图组件、`MapUtils`、`WorldManager` |
| `AnimationService Animation` | 分别缓存身体动作与独立头部资源并解析朝向 | Godot 原生资源 |
| `ItemAssetService Items` | 共享物品素材目录与键索引、按需图集和裁剪纹理，选角预热当前物品 | Godot 原生资源 |

`ConfigService` 显式持有 `MapCfgCache Map`、`ItemCfgCache Item`、`BasicCfgCache Basic` 和 `EquipmentCfgCache Equipment`，构造时按缓存类的文件标注读取平铺于 `res://config` 根目录的全部 JSON，拒绝子目录与未注册文件。单例通过 `Basic.Value` 读取，多例通过各缓存的 `Items` 读取。配置缓存统一使用 `res://schemas` 中同名的 Draft 2020-12 JSON Schema 校验；原始加载只走 Godot `DirAccess` 与 `FileAccess`。每表完整读取后调用 `CfgCache.BuildIndexes()`，全部表及索引就绪后由统一入口调用 `CfgCache.Validate(ConfigService)` 整表重载检查关联；泛型缓存已有的 `Validate(TCfg)` 保留逐行检查。不增加策略目录或检测策略类，不通过全局服务取得其他缓存。缺失、重复、未注册、无效配置、Schema、索引构建或关联错误均导致启动抛异常，保留文件与原因诊断并清理已创建缓存。配置与程序同包发布，释放后服务与已取得缓存都拒绝继续读取。配置及整组服务验收在 Godot headless 执行；普通 .NET 地图用例直接提供 `MapCfg` 纯数据。

主控制器调用无参 `GameServices.Initialize()`。位于 `src/Config/EnvService.cs`、命名空间为 `Rpg.Config` 的无依赖 `EnvService` 先建立统一目录：`res://config`、`res://schemas`、绝对化后的 `user://settings` 和 `user://server`；Config 的公开构造只接收 Env，Setting 接收 Env、Log，MockServerService 接收 Env、Config，Net 接收 MockServerService、EventBus、Log，Session 接收 Net，World 接收 Env、Config、Setting、Session，EventBus、Animation 和 Items 无构造依赖。`Initialize()` 在同一方法体内按 Env、Log、EventBus、Config、Setting、MockServer、Net、Session、World、Animation、Items 顺序显式创建全部十一个服务；不使用容器、反射或自动依赖排序。`NetService` 借用注入的服务器服务建立 TCP 适配，连接释放不释放服务器服务；进程内适配只供显式回归，`SessionService` 自行建立账号缓存。初始化返回所有权句柄；注册前访问、已释放实例注册和重复初始化明确报错。构造失败回收已创建服务；释放时先撤销全部全局入口，再严格按构造的逆序 Items、Animation、World、Session、Net、MockServer、Setting、Config、EventBus、Log、Env 清理。Session 的网络重置仅调用绑定实例，Net 仅使用构造传入的 EventBus，清理不依赖全局入口；不增加 BeforeDispose 阶段。单项失败仍继续清理其他服务，最后报告异常；旧句柄重复释放不影响新注册。内部 `Register` 只验证并接管完整的现成实例组，注册句柄的构造只保存实例，不创建服务。隔离环境和显式传输的服务组装放在 `tests/GameServicesTestFactory.cs`，纯网络和事件测试自持事件实例，缓存相关测试先注册完整服务组并在既有 Godot 测试宿主执行；传输及已有登录响应构造仅为内部验收边界，业务节点不负责组装或释放单个服务。

`MainController` 只协调页面与表现：登录和选角按钮调用 Session 服务，成功后创建相应页面或世界表现；固定帧先推进 Net 再推进 World，再更新角色与摄像机。输入由节点完成视口逆投影，再交给 World 服务限制边界、分配移动序号并同时提交 ECS Action 与网络请求。控制器不再保存目录或网络工厂；`InitializeServices()` 只调用无参启动，场景测试替身覆盖整组初始化以使用独立环境和传输。启动失败和退出共用清理，先撤销 UI 监听、提交拖动设置，再释放服务句柄；未获得句柄的控制器不会释放其他启动流程的服务。

`SessionService.Login` 在请求前清空旧账号、隔离网络代次并创建空的 `LoginCache` 与 `RoleCache`。`LoginCache` 自身在缓存阶段处理首份及后续 `LoginRt`，完整校验摘要后才标记已认证；服务不订阅响应，也不重置正在分发的成功响应。非法摘要不留下有效账号状态或部分数据，并阻止 UI 成功回调；当前尝试的空缓存保留托管订阅，重试、退出或服务释放时回收。UI 只读取 `GameServices.Session.Roles`、`RoleId`、`Role` 和状态标记；缓存容器与已有响应构造均为内部接口。服务绑定创建时的网络及账号代次，旧实例不能操作后来注册的网络或账号。

选角复用账号会话，发送请求前清空角色缓存并隔离旧代次。`RoleCache` 成功初始化后，`MainController` 的选角回调显式调用 `WorldService.InitializeRole()`，调用世界初始化前由 `ItemAssetService` 预热当前背包与装备图标，随后先建立地图实体，再建立角色实体，并选择对应角色设置；随后 UI 创建表现。`MainController` 监听移动响应与主动推送，调用 `WorldService.SyncRole()`，由服务将当前缓存快照转为同步 Action，不允许节点直接写 ECS；同步不依赖 HUD 是否存在。选角失败保留账号摘要并恢复角色列表。返回选角清理世界、角色数据和当前角色设置，保留同一个账号；返回登录由 Session 服务释放账号缓存并清空账号状态，各服务仍常驻到场景结束。背包与公开装备由 `RoleCache` 分别持有 `BagPbData` 和 `RoleWorldEquipmentPbData`，不设置 `DataCache` 或独立 `BagCache`；选角原子初始化全部角色领域和背包，切角同时清空。

所有功能缓存位于 `src/Session/Cache/`，继承 `CacheBase`，通过受保护的 `Subscribe<T>()` 注册缓存阶段监听，由基类私有集合托管句柄。释放幂等取消自身订阅并执行一次 `OnDispose()`，释放后禁止新增订阅。所有 `PbData` 类位于 `src/Session/Data/`，通过自身 `FromPb` 与 `ToPb` 校验、复制数据，不共享可变协议对象。`RoleCache` 分别持有不可变的世界资料、生命魔力状态、位置、公开装备与当前玩家私有 `RolePbData`，各按最后接纳的版本过滤；本地组合为 `RoleWorldStatePbData` 供 World 与 UI 读取，但其 `ToPb()` 仅生成可见世界状态，私有数据须通过 `ToRolePb()` 单独转换。世界分别过滤资料和位置版本，位置推送在确认序号不变时仍可纠偏。

事件总线位于 `src/Services/EventBusService.cs`，参数与局部变量使用 `eventBus`、字段使用 `_eventBus`。`GameServices.EventBus` 是独立的 `EventBusService : Service`，直接包含分发实现，没有额外转发包装。构造方法不对外公开，GameServices 是唯一创建位置；事件实例与其他服务统一保存在 Registration，不保留独立静态字段或提前注册。Net 通过构造绑定 EventBus；缓存每次订阅通过 GameServices.EventBus 取用服务，不持有事件服务引用、不提供事件传参构造，仅托管订阅句柄。缓存随 Session 创建与释放，必须在完整注册后才订阅，释放已有句柄不依赖全局入口。UI 从 GameServices.EventBus 访问，消费者不创建私有事件域。Current 与全部访问器仅检查完整注册是否存在，IsInitialized 仅表示注册状态；单个服务提前释放后仍可取得该注册实例，服务业务方法自行检查释放状态，直到整体句柄撤销注册。独立网络保留各自发送队列，但共享该事件服务；单独释放网络不清除事件监听，整体退出在全部消费者之后、环境服务之前释放事件服务并清除全部监听和队列。分发规则：先 Cache 后 Normal，同阶段按注册顺序，每条消息使用监听快照。新增监听处理后续消息，已取消监听立即跳过，嵌套发布排队。缓存失败跳过该消息的普通阶段，普通监听失败不阻止其他监听。业务协议订阅仅允许出现在 `CacheBase` 实现类与 Godot UI 类；基类负责缓存阶段注册和句柄托管。`LoginCache` 在 Cache 阶段更新认证状态，`MainController` 在 Normal 阶段协调世界操作，服务不注册业务监听或持有订阅句柄。网络的传输层字节接收属于 `INetTransport` 契约，不承担业务协议订阅。网络编解码与传输错误通过 `NetService.Error`、监听错误通过 `GameServices.EventBus.Error` 报告至页面或 HUD。会话重置递增信封代次、清理队列和服务端未确认移动，旧消息不能进入后续角色。

`NetService.Send(IMessage)` 调用时序列化并入队，回调内新增请求留到下一帧发送。`INetTransport` 隔离具体传输，`MockTransport` 在同进程连接 `MockServer`，双向仅交换 protobuf 字节。真实连接以后提供新的传输适配器，世界与 UI 不接触传输细节。

本地系统设置保存到 `user://settings/system.json`，角色设置按 `role_id` 保存到 `user://settings/roles/`。`SettingService` 独占当前设置，提供当前值、分辨率、音量、技能栏、HUD 布局、普通窗口坐标和快捷键操作；原始读写方法保持私有。离开角色或关闭服务时提交未保存布局并清空角色设置。窗口、声音和 HUD 实际呈现由节点应用，设置不作为服务器权威数据。
声音设置保留旧 `MasterVolume` 字段作为 BGM 音量来源，新增 `EffectsVolume`；两路分别应用到 BGM、SFX 总线。声音页预览仅改变总线，保存时一次提交两路值，关闭或离开世界时恢复保存值。`UiSoundPlayer` 属于主场景，复用短音效播放器并监听动态按钮与页签，离场时停止播放。登录及进入游戏成功使用 1005；菜单、开窗、角色外观预览、确认、取消和关闭使用 1003；物品交互保留原音效。主场景的 `BgmPlayer` 使用 BGM 总线与单个循环播放器：登录、选角及角色创建页面播放 M001，世界播放当前地图配置的曲目；同曲不重启，返回页面切回 M001，离场停止。
显示设置另存窗口模式：普通窗口、Godot 独占全屏、Godot 无边框全屏。保存的宽高始终是普通窗口尺寸；全屏实际尺寸由显示器决定。显示页先选模式，普通窗口可选分辨率；两种全屏只显示当前显示器分辨率并禁用选择，切回窗口时恢复预览中的窗口尺寸。选项变更直接应用到 Godot 窗口但不写盘；“保存”成功后才更新基线，关闭面板或离开世界恢复最近保存值。SettingService 写入候选设置成功后再替换内存值。启动和显示页预览共用窗口切换逻辑，返回普通窗口时恢复尺寸并居中；旧系统设置缺少模式或模式无效时采用普通窗口。

### 配置编写规范

- `config/` 不分目录，文件名唯一；单例根为 `{ ... }`，多例根为 `[{ ... }, { ... }]`。JSON 只存配置数据，不写 `kind`、文件名、缓存类型、主键描述等元数据，不增加外层 `items` 包装。
- 数据统一命名 `XxxCfg`：单例承载整个对象，多例承载数组中的一行。文件放在 `src/Config/Data/`，使用初始化属性与简明中文注释。
- 缓存统一命名 `XxxCfgCache`，放在 `src/Config/Cache/`，类上加 `[CfgFile("xxx.json")]`。单例继承 `SingleCfgCache<XxxCfg>`，通过 `Value` 读取；多例继承 `MultiCfgCache<TKey, XxxCfg>`，通过只读 `Items` 字典读取。
- 多例数据恰有一个公开可读实例属性标注 `[CfgKey]`，其类型与 `TKey` 一致；支持整型和字符串等可用作字典键的类型，不隐式使用 `Id`。缺失、null 或重复主键抛异常。JSON 字段默认使用 camelCase，可用 `JsonPropertyName` 明确别名。
- 装备配置默认约定 `equipment.id` 与对应 `item.id` 使用同一数值，便于查表；这不是校验限制，实际关联始终以 `item.relatedId` 为准，允许不同编号和共享装备配置。
- 每个文件配套 `schemas/xxx.schema.json`。`CfgCache` 统一读取、Schema 校验、加载后索引、整表关联校验和生命周期；两个泛型基类决定对象或数组的解析与存储，具体缓存通过 `Validate(TCfg)` 补充逐行约束，通过 `Validate(ConfigService)` 补充跨表约束，通过 `BuildIndexes()` 构建索引，`ClearIndexes()` 清理派生索引。新增缓存后在 ConfigService 显式声明引用并加入统一加载集合，禁止添加按业务类型分派的解析分支。

`config/map.json` 对应 `MapCfgCache : MultiCfgCache<int, MapCfg>`，地图条目和初始地图引用保持原有校验；初始地图关联检查位于 `BasicCfgCache.Validate(ConfigService)`。`config/item.json` 对应 `ItemCfgCache : MultiCfgCache<int, ItemCfg>`，保留 1012—1018 七件测试装备及其名称、分类、堆叠和图标。`Category` 为非空 `ItemCategory` 枚举（1=Weapon、2=Armor），`SubCategory` 为非空 `ItemSubCategory` 枚举（枪1、剑2、双手武器/工具4、拳刃8、法杖16、暗器32、盾牌105），JSON 仍保存数字。缺失、null、零、未定义枚举和大小类错配均阻止启动，其他分类按后续需求确认后新增。`relatedId` 为必填非负整数，装备道具必须关联存在的装备配置。`GetByCategory` 和 `GetByCategoryAndSubCategory` 使用加载后构建的单键及双键索引，内部值为 `List<ItemCfg>`；查询返回保留配置顺序的列表副本，未命中为空列表，释放后拒绝查询。

`config/equipment.json` 由 `EquipmentCfgCache` 加载，每行声明正整数 `id` 和 `appearanceId`，后者显式关联项目内持握资源。默认约定装备与道具 id 同值，但实际关联由 `ItemCfg.RelatedId` 决定，允许不同编号与共享配置；外观编号也不要求与两张表主键相同。缓存逐行验证男女七类动作资源存在，整表 `Validate(ConfigService)` 检查全部武器和防具的引用。十一种 `EquipmentSlot` 使用稳定枚举；`EquipmentSlotUtil.CanEquip` 在代码中绑定全部武器到主手、盾牌到副手。普通剑可与盾牌共存，其他武器占用双手；`OccupiesBothHands` 集中判断跨槽约束，`HasPairedAppearance` 声明拳刃和暗器的成对外观。`TryGetSlot` 推荐武器主手、盾牌副手；装备表不承载槽位和属性。

地图条目的可选 `bgmTrackId` 保存曲目编号；地图 1 配置 M015a，未配置的地图不播放地图 BGM。Schema 限制编号格式，曲目资源位于 `assets/audio/music/`。

```csharp
var map = GameServices.Config.Map.Items[mapId];
if (GameServices.Config.Item.Items.TryGetValue(id, out var item))
{
    var maxStack = item.MaxStack;
}
```

## 协议与模拟服务端

`proto/` 是唯一协议源。`message.proto` 定义 generation、request_id、message_type、payload 四字段信封与统一错误；`login.proto` 定义登录和踢出；`role.proto` 定义摘要、角色状态及创建删除；`world.proto` 定义进入离开、移动、世界帧和重同步；`map.proto` 定义地图对象与差集；`bag.proto` 定义背包、移动、穿脱、丢弃、拾取与本人物品通知；`gm.proto` 定义开发命令。内部消息位于 `proto/actor/`，不注册到外部方向表。请求无后缀，回复以 Rt 结尾，通知以 Notice 结尾，数据及枚举以 Pb 结尾。

`dotnet build rpg.csproj` 通过 Grpc.Tools 生成 `src/Protocol/Generated`（Rpg.Protocol / package rpg）与 `src/Server/ActorProtocol/Generated`（Rpg.Server.ActorProtocol / package rpg.actor）。不直接修改生成代码。消息体展开，字段两空格缩进，各定义之间空一行。


UI 通过 EventBus 普通阶段监听读取服务快照；缓存通过 CacheBase 注册缓存阶段监听，完整验证后原子接纳。SessionService 和 WorldService 不持有协议监听。

## 输入、世界与表现

### Component 编写规范

`src/World/Components/` 中的组件统一声明为普通 `internal sealed class`，类名后不写构造参数列表，不使用主构造函数或位置式 `record`。所有组件都在类内部显式声明 `public` 构造方法，使用 `{ ... }` 块体并添加简明中文注释；无外部参数时也声明无参构造方法，不依赖隐式构造。字段与属性的初始化赋值统一放在构造方法内，保持必需参数、只读身份和可变运行状态的边界；调用时使用 `new DataXxxComponent(...)`，无参时保留 `()`。此约定适用于 ECS Component。组件只承载数据，不持有服务或管理器，不实现业务方法；行为由系统处理。

```csharp
/// <summary>角色实体持有的当前位置组件。</summary>
internal sealed class DataPositionComponent
{
    /// <summary>角色在世界中的像素坐标。</summary>
    public Vector2 Value;

    /// <summary>按初始位置构造组件。</summary>
    public DataPositionComponent(Vector2 value)
    {
        Value = value;
    }
}
```

### 世界行为与呈现

地图分块在首次绘制时将 8×8 格地表构建为一个带顶点颜色的二维 ArrayMesh，网格线通过一次 DrawMultiline 提交；每块只提交两条画布绘制命令，继续复用静态几何与可见分块，离树释放所拥有的网格。

`WorldService` 直接把未被 UI 消费的 Godot 点击转换为移动 Action，不经过单独的输入转发类。`WorldManager` 是 ECS 的唯一外部写入入口：输入 Action、缓存状态同步 Action 排队后由 Godot 固定帧处理。初始化顺序为地图世界实体、角色实体。轻量 ECS 由整数实体 ID、组件仓库和固定顺序系统组成；首期移动系统按直线推进并接受权威位置修正。

`DataMapComponent` 只保存地图宽高、瓦片大小、默认地块和不可变稀疏覆盖索引，不持有配置服务或业务对象。`MapUtils` 位于 `src/World/Utils/`，以显式传入的地图组件完成边界裁剪、地块读取和可见格计算；它是按需调用的静态纯计算工具。ECS System 使用实例承担实体行为与系统调度，无状态的地图计算归入 Utils。`WorldService` 从配置构建只读数据用于查询，并通过 `InitializeWorldAction` 交给世界实体；`WorldManager` 处理移动及权威修正时从实体读取该组件并调用同一工具。`IsometricProjection` 在世界表现与地图点击边界做双向转换，ECS、配置、雷达和大地图继续使用逻辑坐标。地图表现将视口附近地块划为 8×8 格分块，各分块一次绘制 2:1 等轴菱形并复用画布命令；相机或视口跨越分块范围时只增删差异分块，不创建百万个瓦片节点。角色与摄像机使用同一投影。HUD、小地图、GM 和系统面板放在独立 `CanvasLayer`，不受世界摄像机变换影响，从 `GameServices.Config`、`Session` 与 `Setting` 读取所需数据。GM 面板从设置服务获取系统和当前角色设置路径，交由桌面文件管理器定位。

角色数据的 `RoleGender` 随选角与会话缓存进入玩家 entity。`DataRoleComponent` 保存角色 ID、性别和头型，`DataActorComponent` 保存动作语义、逻辑朝向与显式动作请求版本；`MovementSystem` 根据实际移动切换跑步、到达后站立并保留朝向，`SetRoleMotionAction` 可请求其他基础身体动作并停止本地移动。角色属性同步按权威 revision 接纳，位置修正继续按移动确认序号过滤。`WorldManager.ActorState` 只提供不可变快照，不暴露可写组件。

`ActorView.Present` 读取快照并投影位置和朝向，表现层 `AnimationService` 按性别和动作缓存 `DataActorAnimation`，按性别和头型编号缓存 `DataActorHead`。男女共用各自身体动作集，148 套头部与身体分别存储，换头不复制身体图集，不生成头身组合资源。每个身体动作包含五方向 `SpriteFrames`、原始世界定位点和方向/帧顺序的 `HeadAttachments`；每个头型包含五方向纹理与各自脖颈连接点。`ActorView` 在同一个镜像容器下放置身体 `AnimatedSprite2D` 与头部 `Sprite2D`，身体帧变化时按“身体挂接点 − 身体世界锚点 − 头部连接点”更新头部位置。两层共用朝向、镜像和身体时钟，改向保持帧进度，动作播放不改变世界逻辑。

`tools/import_actor_animations.ps1 -SourcePath <解码帧目录> -RawPath <原始目录>` 调用 `import_actor_layers.py` 重建全部 26 个身体动作、605 帧头部及双手挂接点与 148 套独立头部，再调用 `import_actor_weapon.py` 导入七件测试装备；`decode_actor_heads.py` 只解码原始像素及定位点。稳定编号按原始数字文件名排序，男女各 74，来源及哈希记录在 `assets/characters/head_composition.json`。创角只提供编号 9–72，对应原始 `head1101` 至 `head8801` 及配套 DT 头像；旧角色的 1–8、73–74 仍可加载并以头部正面帧作头像。运行与导出只依赖项目资源。当前头部包含配套头发，未拆分染色或独立发型；后续衣服、装备属性及战斗规则待定。

装备表现由独立公开装备快照驱动，普通空槽隐藏图层，拳刃/暗器的空副手槽由主手派生左手外观。`WorldService` 在固定帧发现已验证 `RoleWorldEquipmentPbData` 引用变化时提交 `SyncEquipmentAction`，ECS 按装备版本更新主副手道具编号，`DataActorState` 输出只读编号。`ActorView` 通过道具 `RelatedId` 取得装备 `AppearanceId`，`AnimationService.GetWeapon` 按外观、性别和动作缓存 `DataActorWeapon`；实际身体动作类别不变时切换装备保留身体时钟，卸下清除对应纹理和可见性。

`tools/import_actor_weapon.py --raw <原始目录>` 导入1012→weaponr01、1013→weaponr41、1014→weaponl11、1015→weaponr21、1016→weaponr31、1017→weaponr51/weaponl51、1018→weaponl105/weaponr105。每性别提供13个入口，七件及双手变体合计234入口、3150帧引用；原生纹理不拆段、不补柄，缺失动作共享站立纹理。`SourceAction`记录实际来源动作编号，`AttackMotion`声明剑类第1组、枪杖第2组、拳刃飞刀第3组，盾牌不指定攻击类别。原客户端取证地址、文件哈希及边界见 `docs/source-actor-composition.md`。

`AnimationService.ResolveMotion`仅解析表现：主手优先，空主手取副手武器类别，空手保留请求；攻击与准备分别选择对应身体动作，不改写ECS。身体为唯一时钟，武器以身体帧对来源帧数取余，缺少动作回退站立；更换装备实际身体动作不变时保留播放进度，类别改变时切换匹配身体。

`DataActorComposition`持有原客户端八方向四部件基础顺序及男女13动作完整50槽层次。男性用普通段，女性用g段。`ActorView`按当前身体实际动作、源方向及身体帧升序稳定排序身体、头、副手、主手；零值保留八方向基础次序，正值较晚绘制。武器即使纹理回退站立，也使用当前身体动作层次。排序只改变角色内部兄弟顺序，保持世界Y排序。

身体 `LeftHandAttachments` / `RightHandAttachments`分别读取源像素块尾+28/+36的坐标。主手使用源右手原画及挂点，副手使用源左手原画及挂点，Facing整体镜像不交换源手或装备槽。位置为身体挂点−身体锚点−装备连接点。原手形切口由来源身体与武器自然组合，不再使用跨手补柄或枪杖推测分段。

`tools/verify_actor_composition.py`独立解析原始DIMG/INI并提取指定客户端的基础次序，不调用生产解码、排序或帧采样函数；核验全部234入口原像素与锚点，生成C#黄金摘要。全部角色最近邻纹理关闭 `fix_alpha_border` 与 `filter_clip`，保留低alpha原颜色及最外侧像素；透明GPU输出转换为直通alpha后保存审查PNG。`ActorSourceCompositionAssertions`验证9种合法组合、男女、13请求、八方向全部8728身体帧的动作、源手、挂点、排序与播放保持；`ActorWeaponHandAssertions`检查全部来源像素摘要。独立参考合成另与正式GPU逐帧输出比较，覆盖默认头型1；其他头型保留原有148套资源与头部契约验收。

选角与创建页面复用 `RoleScreenControl` 的全屏布局和 `RolePreviewControl` 的独立角色预览。选角页右侧为滚动列表，点击条目只更新中央角色，预览下方独立进入按钮才请求选角；创建按钮在列表下方，返回登录在屏幕右下角。创建页右侧顶部为男女互斥按钮，下方滚动平铺当前性别编号 9–72 的 64 套 DT 头像缩略图，不使用下拉菜单。`RoleCreateControl` 分别保存男女的头型、朝向与命名草稿，切换恢复对应状态且互不覆盖。中央预览下方提供旋转及创建按钮；点击创建打开角色名确认窗口，确认才提交请求，取消不提交，失败保留窗口与草稿。预览复用独立 `ActorView`，不创建 ECS 世界；右侧网格在较小逻辑视口减少列数。创建/删除等待期间禁用操作，失败恢复可操作状态。选角、创角和装备窗口共用朝屏幕右下方（↘）的默认朝向（`DefaultDirection = 1`）；左侧按钮使人物逆时针自转，右侧使人物顺时针自转，按钮图案不变，展示朝向与世界朝向独立。两类成功响应均包含完整摘要列表，`LoginCache` 在 Cache 阶段完整校验后原子替换，UI 在 Normal 阶段只读 `Session.Roles` 刷新；允许删空和重启保持空列表，删除不清理角色本地设置文件。

```text
map.json -> GameServices.Config -> World / 地图表现 / UI
UI -> GameServices.Session -> GameServices.Net -> protobuf 字节 -> MockServer -> 内存缓存 -> 检测/关闭节点 -> JSON
MockServer -> protobuf 字节 -> GameServices.EventBus(Cache) -> Session 内部账号缓存
                           -> GameServices.EventBus(Normal) -> MainController -> World 同步 Action -> ECS -> 只读表现快照
                                                  -> UI 刷新
Godot 输入 -> 等轴逆投影 -> GameServices.World -> 移动 Action / 会话移动请求
本地设置 <-> GameServices.Setting -> UI
动画资源 -> GameServices.Animation -> ActorView
```

## 鼠标与地面点击反馈

`PointerCursor` 同时将 `assets/pointer/edit.png` 注册到原生 Ibeam 槽位，登录账号、密码和其他文本输入区域自动使用配套 Edit 鼠标。Edit.gsa 为 9×15 单帧，独立按 32/15 倍最近邻缩放至 19×32，原热点 (5,8) 同步缩放至约 (10.67,17.07)，普通与 Fetch 仍保持 1.25 倍。登录账号与密码归入 LoginFields 容器，容器沿用原间距并使用 Ibeam，使中间窄缝不短暂切为普通手指。形状切换由 Godot 原生输入区域决定，点击编辑和密码遮蔽行为保持；退出主场景同时清除 Arrow 与 Ibeam 注册。导入工具将 Edit 来源、哈希及热点记录在 source.json，既有指针 smoke 覆盖登录悬停、输入和移出恢复；硬件实际像素仍需图形窗口验收。

`MainController` 启动时创建 `PointerCursor`，将 `assets/pointer/normal.png` 及 `click_0.png`、`click_1.png` 最近邻放大 1.25 倍至 51×44，原配置热点 (2, 2) 同步缩放为 (2.5, 2.5)。左键按下立即播放 Click 两帧（每帧 0.08 秒）；实际按键状态与视觉播放状态分离，同帧短按也播完两帧后恢复 Normal，长按保持末帧至释放后立即恢复，连续点击重播，Fetch 优先且失焦清除反馈。业务输入不等待视觉播放；在 GUI 消费前观察输入，所有页面及物品点击均生效，退出场景时恢复系统鼠标。`tools/import_pointer_feedback.py --source <art_export目录>` 严格解码 IANI v1 图块与帧数；Normal 按原配置 holdframe=1 取首帧，Click 导入全部两帧，circle 拼出完整 68×41 单帧画布，来源和哈希记录在 `assets/pointer/source.json`。运行不依赖外部素材目录。

世界接受地面左键移动后，`MainController` 在逆投影所得世界坐标创建 `GroundClickView`，完整光圈居中显示在地图上方、角色下方；Tween 在 0.5 秒内将光圈从原尺寸的 25% 等比扩散到 125%，同时淡出并释放，连续点击分别完成反馈，退出世界随父节点清理。UI 消费输入、右键和松开均不创建反馈。circle 的原版用途尚未确认，扩散比例与淡出时长为本项目表现参数。`tests/pointer_feedback_smoke.tscn` 纳入完整检查，验证资源完整性、真实视口点击、摄像机移动、连续等比扩散、淡出释放和 UI 输入隔离；硬件鼠标的实际屏幕显示需要 Windows 图形窗口目视验收。

## 弹窗与 HUD 窗口边界

角色、大地图、GM 和系统设置复用 `ViewportPanelControl`：使用左上原点与实际视口中心计算位置，打开及每帧布局都将外框限制到视口内。标题和右侧关闭按钮固定，业务内容进入双向滚动容器；小窗口不让内容的最小尺寸撑大外框。道具图鉴同样将筛选与详情内容置于独立外层滚动区，保持标题和关闭按钮可访问。

`GameHud` 对加载设置、拖动及窗口缩放按整个 HUD 尺寸限制位置；内容延迟排版造成边缘越界时也重新布局。拖动把手的局部鼠标坐标先转到视口坐标再计算偏移，避免按下后跳动，不改角色设置格式。`tests/ui_bounds_smoke.tscn` 验证 1920×1080、1280×720、640×360、480×270 的居中、双向滚动、关闭可点击、负位置恢复、图鉴窄窗口、HUD 拖动与重启加载边缘设置。

## GM 道具素材图鉴

GM 面板并排平铺“打开用户数据目录”和“查看道具图鉴”两个同级按钮，不再展示系统与角色设置文件路径。用户数据目录是 Godot `user://` 对应的应用可写数据根目录，存放本地设置和模拟服务端存档；点击时通过 `ProjectSettings.GlobalizePath("user://")` 动态解析，并由系统文件管理器打开目录内的内容，失败显示错误。

GM 的“查看道具图鉴”打开独立 `ItemCatalogControl`，与其他 HUD 面板互斥。控件首次打开从 `GameServices.Items` 取得共享素材目录和索引，并读取 `definitions.json` 具名道具快照；默认道具清单模式按照参考导出页的 `display_category` 分为武器、防具／饰品、其他装备、宠物、收集品、消耗品，再按父类型内的 `type_code/type_name` 筛选子类型，支持名称、ID和说明搜索。素材预览模式保留四个图片来源分类与来源编号搜索。两种模式每页至多创建48个条目，通过共享服务按需加载图集，详情独立滚动；切换父类型重置子类型，切换模式、分类或查询重置分页和详情。图鉴不持有独立图片索引或会话状态。正式运行及 Windows 导出只依赖项目内资源，导出预设包含素材及道具目录 JSON。

`python tools/import_item_catalog.py --source <art_export目录>` 使用 Pillow 将四个源目录的全部 PNG 无损打包为28张2048×2048图集，保留来源相对路径、原始编号、SHA256、尺寸和透明裁剪偏移。素材目录共22,872条：道具小图标6,612、道具大图6,097、左手武器部件1,451、右手武器部件8,712。来源为 `item/item`、`item/itempic`、`avatar/weaponl`、`avatar/weaponr`；大小图分别保留，武器包含动作与方向变体且仅展示已解码首帧，不代表独立武器数量。

`python tools/import_item_definitions.py --source <item_export目录>` 从用户指定导出页同目录的 `items.json` 导入21,792条具名道具，输出 `definitions.json` 与 `definitions_source.json` 来源哈希证据。武器879、防具／饰品8,286、其他装备30、宠物1、收集品1,073、消耗品11,523；以来源表和行号作为唯一键，不合并重复ID。21,315条图标按 `icon_candidate` 路径精确关联现有素材，477条缺图记录保留并显示占位。详情保留名称、原始类型码、说明、背包归属、分类与子类型确认程度以及来源行。装备语义及子类型名称沿用来源推定，不将客户端区间0/1直接解释为武器/防具；图标保持候选性质，条目不代表当前服务器投放或已实现玩法。图鉴不提供背包和穿戴。

`tests/item_catalog_smoke.tscn` 检查全部素材区域、六父类型与全部子类型数量和筛选、名称/ID/说明检索、项链区间误分类回归、重复行、缺图预览隔离、原素材模式、真实GM入口、分页、关闭重开、小窗口布局与输入隔离，纳入 `tools/test.ps1`；图形模式添加 `-- --capture-catalog` 可输出素材、剑类及项链分类截图到 `build/`。

## 背包与聊天 GM

候选框显示时，主键盘回车与数字小键盘回车优先补全当前选中项并消费按键，不进入发送链路；候选隐藏后两种回车均可提交。`tests/bag_gm_smoke.tscn` 覆盖连续确认命令与非首个道具、聊天记录及背包版本不变，以及隐藏候选后的默认数量和显式数量提交。

`config/basic.json` 通过 `BasicCfgCache : SingleCfgCache<BasicCfg>` 加载，`Config.Basic.Value` 提供 8 列、5 行基础背包。角色 `BagExtraRows` 默认为 0，容量计算检查非负扩容和整数溢出。角色 Handler 通过 Context.Config 取得配置，物品规则校验候选后提交模块缓存，Handler 不执行 IO。

服务端实例持久化使用 `.item.json` 与 `.equipment.json` 多例表，容量扩容和 NextInstanceId 由角色单例保存。实例 ID 在角色内递增不复用；未穿实例按零起格子排列，已穿实例 Equipped 为 true、SlotIndex 为 0。`BagPb.Items` 返回全部私有实例，ItemPb 的 equipped 区分穿戴，oneof payload 预留空 EquipmentPb，不向客户端传递装备槽位。`BagPbData.AllItems` 拥有完整列表，Items 仅提供未穿格子视图，Equipment 根据私有实例及配置推导操作视图。选角与 `RoleItemsChangedNotice` 同时携带公开配置 ID 和私有完整实例；缓存先校验容量、数量、实例/未穿格子唯一性及展示一致性，再按消息顺序原子替换。移动回复只确认 action_id，权威位置通过世界帧同步，其他角色和旧代次消息隔离。

`ChatControl` 常驻 HUD 左下角，与互斥弹窗独立。回车或发送按钮提交去首尾空白的输入，斜杠开头转为去掉斜杠的 Gm 请求，普通文字仅提示“聊天暂未开放”。服务端仅支持 `/item <ItemId> [Count]`，默认数量 1，仅发给当前认证角色；按格子补满已有堆叠再占空格，遵守 MaxStack。全部数量放不下或参数错误均不改变内存、存档、实例编号与版本。成功提交内存后标记背包待写并推送背包，再返回聊天普通文本反馈。移动确认基于最新角色数据保存，不覆盖物品。

聊天不显示初始说明或固定占位文字。聚焦输入 `/` 时显示命令候选；`/item ` 直接读取 `Config.Item.Items`，按 ID 排序展示 ID 与名称并按前缀筛选。输入上方的候选浮层限制在视口内且可滚动，点击或上下键加 Tab 只补全文本，Escape 和失焦隐藏。有配置的道具 ID 后加空格，在实际文本末尾覆盖不消费输入的灰色“数量”提示；显式提供正 Int32 数量且参数完整时整行文字变绿，删除或改成非法参数立即恢复。绿色只表示格式与道具 ID 有效，数量省略仍默认为 1。`tests/bag_gm_smoke.tscn` 同时覆盖提示、补全、颜色恢复、输入隔离及小窗口，图形运行附加 `-- --capture-chat` 可保存四个输入阶段截图到 `build/chat-*.png`。

`BagControl` 点击打开时读取最新缓存，显示全部网格、图标和数量（含 1），悬停显示配置 Name；打开期间随 Notice 刷新。七件测试装备显式绑定现有图集素材键，缺失或未知图标显示占位；滚动区支持小窗口。`tests/bag_gm_smoke.tscn` 验证真实视口发送、回车、名称提示、输入隔离、关闭重开、缺图、小窗口和重启持久化；Windows 图形运行附加 `-- --capture-bag` 可保存 `build/bag-gm.png`。暂不实现普通聊天、主动拆分、使用或装备属性。

`BagSlotControl` 在左键按下时立即拿起物品，松开保持，目标格再次按下时提交；`BagControl` 用独立 CanvasLayer 通过共用内容工厂生成图标和数量预览，图标中心对齐鼠标热点，不受滚动裁剪。名称由格子悬停即时显示普通文本浮层，移出或拿起隐藏；不使用默认延迟 Tooltip。空白点击取消并消费世界输入；空格整份移动，同类未满目标优先合并，剩余留原格，其他目标交换。完全合并只移除耗尽源实例，不回收编号。`MoveItem` 携带实例和目标格，当前角色从连接绑定取得，按最新服务端状态执行，服务端校验后提交内存并标记背包待写，再发送完整背包推送和 `MoveItemRt`；同格不修改状态。UI 不乐观修改缓存，失败在背包显示原因，关闭、离树、刷新、切角和旧版本都会使原拖放失效。`tests/item_movement_smoke.tscn` 通过真实鼠标事件验证两次点击、松开保持、中心吸附、即时名称与按下指针、空格移动、交换、部分/完全合并、取消、过期、480×270 下滚动到末格、失败反馈、世界输入隔离、切角及重启。

## 当前限制

当前使用 admin、demo1、demo2 / 123 演示认证。已支持真实 TCP、多人移动、背包整理、主副手穿脱、丢弃、销毁与走近拾取；尚未实现正式认证、寻路、战斗技能、物品使用或主动拆分。怪物仅展示，不具备行为。JSON 存档只支持单实例写入，不提供多个服务器进程共写协调。正式玩法、美术及故事仍待确定。

## 测试执行

`tests/movement_performance.tscn` 使用隔离临时存档启动真实主控制器，自动登录、选角、静止与连续改目标移动，输出帧时间分位数、最大值、超过 50 ms 的帧数、引擎耗时监控及托管分配。Windows 图形模式以 `--disable-vsync --max-fps 120` 启动该场景可复测；可在 `--` 后传 `--capture=<绝对 PNG 路径>` 保存静止画面。引擎耗时监控约每秒刷新，可能带入上一阶段，不能视为每帧 CPU 时间；帧间隔以 Stopwatch 独立采样。性能数值不设跨硬件硬阈值，headless 只用于功能回归。

`powershell -ExecutionPolicy Bypass -File tools/test.ps1` 执行项目检查与完整测试：普通用例通过 `dotnet test` 运行；标记 `Runtime=Godot` 的配置和整组服务用例通过 headless 测试场景及现有 xUnit 运行器执行；此外运行身体动画与角色管理场景，验证原始头发像素位置、全部头型/动作/方向及实际按钮交互。`ClientTests.runsettings` 默认排除需要引擎的用例，单独运行 `dotnet test` 只执行普通部分。测试程序集及运行器依赖由测试场景加载，不加入正式项目依赖；失败或未发现 Godot 用例时返回非零退出码。

## 控制台日志

`GameServices.Log` 持有 `LogService`，在 Env 后创建、Env 前释放，构造绑定 EnvService。GM 面板的“控制台日志”开关修改 `Env.ConsoleLogEnabled`，直接保存至用户数据目录 `user://.env` 的 `RPG_CONSOLE_LOG_ENABLED=true/false`。启动直接读取该文件，未设置或无效时默认关闭，支持忽略首尾空白、大小写的 true/false、1/0 与成对引号。重建 HUD 保留，重启重新读取文件。保存保留其他键和注释、合并重复日志键，通过同目录临时文件替换后更新内存；失败显示原因且控件恢复原值。EnvService 直接使用 System.IO，不依赖 LogService 或 GameServices；.env 与临时文件不输出日志，不读写系统环境变量，也不写游戏设置或角色存档。隔离测试可指定独立 .env 路径，未指定时只使用内存。关闭时跳过日志专用的请求解码、消息序列化、IO 压缩和字符串拼接，不补发历史日志；消息接口直接接收 IMessage，IO 复用业务写盘文本，无需为每条日志分配内容回调。必要的业务解析、存档序列化和错误报告不受影响。

NetService 与 SettingService 通过构造绑定日志。当前服务器存档存储不输出成功 IO 日志。日志开启时，请求在调用传输发送前记录，表示发送尝试，传输失败仍保留并沿用网络错误报告；当前代次响应和推送在解析成功后、发布前记录。三类消息分别使用 `[Client][Request]`、`[Client][Response]`、`[Client][Notice]` 前缀并附协议名和 protobuf JSON；只有 Request 标记为绿色、Response/Notice 标记为橙色。成功提交设置和服务端角色 JSON 使用 `[Client][IO][filename]` 与 `[Server][IO][filename]`，附实际写入 JSON 的紧凑单行表示，文件格式保持原样；读取、失败和临时文件不输出 IO。消息 JSON 字符串中的方括号使用 Unicode 转义，IO 走纯文本输出，输出故障不改变业务结果。

## 服务端 Actor 与消息

服务器按账号、角色和地图分片，以 Runtime 的类型化邮箱和 ModuleActor 串行处理；首次消息初始化，关闭排空。业务处理器位于 Modules/<模块>/Handler，跨 Actor 步骤在 Async 中通过 Context.Actors 编排，调用顺序 Login→Role→World，禁止循环 Ask。连接认证与控制元数据使用 DataActorCall，业务 proto 直接进入 Handler。详细边界见 [Server 框架](server.md)。

## 服务端领域存档

账号摘要由 LoginStorage 写入 `<username>.role_summary.json` 数组。RoleStorage 保存 `<roleId>.role.json`、`.item.json` 与 `.equipment.json` 三表；创建仅产生摘要，首次进入创建并保存三表。角色编号由独立持久化序列预留，与账号无关，不复用。已有角色完整加载，部分缺表或损坏拒绝进入。WorldStorage 保存 maps/<mapId>/manifest.json 与版本块。模块 Context 只引用所属 Cache，各 DataCache 保存不可变值并以内容基线跟踪待写。

存档根为 EnvService.ServerDirectory，默认 `user://server`。启动先提升原版本子目录，再恢复未完成批次；不迁移已经删除的旧服务器存档格式。

## 服务端缓存落地调度

MockServer 自有后台时钟推进权威移动与周期保存。默认五秒检测，0 禁用周期，显式写回及正常关闭仍保存。保存由邮箱捕获不可变快照，邮箱外完成物品专用共同批次，再接纳实际保存基线；期间的新移动仍保持待写。角色三表、账号摘要、地图清单通过 items.pending.json 共同回滚，地图旧块延后清理。失败保留内存与待写，可修复后重试；强杀可丢失未落盘变化。

本机 TCP 写回先等此前响应屏障，纯客户端由服务器自行保存，无保存网络协议。关闭依次停止时钟和外部接纳，排空请求及调度、保存、停止 Role→World→Login。

## 多地图状态与九块 AOI

地图以数值 ID 寻址，当前地图为 1，chunkSizeTiles=32；静态绘制仍使用 8 格分块。WorldService 构造只绑定 Config/Setting/Session，进入角色时按 Role.MapId 建立当前 DataMapComponent，离开清空。MapConfig 是当前选图入口；Session.Map 保留已校验的权威 AOI 快照，固定帧通过 SyncMapObjectsAction 同步为动态 ECS entity，MapObjects 返回由组件生成的只读状态。表现节点在世界未就绪或离开后等待释放期间跳过地图查询。

角色 MapId 必须显式保存，缺失或无效时拒绝加载；新角色使用 basic.initialMapId。当前地图身份位于 RoleWorldPositionPb，PbData 保持独立转换。MockServer 持有不可变地图规格与只读道具配置，验证移动目标的地图边界，不依赖客户端配置服务。

WorldActor 按地图 ID 独占 WorldCache、公开角色与对象缓存，DataWorldLayout 保存固定规格，DataWorldObject 保存独立对象数据。WorldStorage 只负责文件。公开角色和对象均用块索引定位观察范围；对象变化标记受影响观察者，世界帧向其发送对象增删。

文件为 user://server/maps/<mapId>/manifest.json 与 chunks/<x>_<y>.<commitId>.json。版本均为 formatVersion=1，清单保存地图身份、布局、提交 GUID 与有效块引用；块保存坐标、提交 GUID 和完整对象。每轮只写变化的非空块，空块移除引用，最后原子替换清单。清单是唯一提交点，跨块移动两侧共同生效；中断候选不参与恢复。已有清单损坏、缺失引用、重复对象或布局不符明确报错。成功提交后清理旧版本，清理失败报告并在下一检测重试，不撤销已提交状态。文件替换沿用单进程及操作系统持久性边界。

地图与角色共用已有检测周期，0 禁用周期，正常关闭补写；切换角色及返回登录只清理观察者而保留地图内存。每个地图/角色独立尝试落地，最后汇总错误。服务器存档不输出成功 IO 日志。

服务器按权威位置选择中心及周围八个有效块。进入响应带初始地图，随后 sequence=1 全量世界帧建立移动权限；跨界发送进入块快照及离开坐标，交集复用。对象 revision、世界 sequence 和磁盘 commitId 各自独立。

Session 持有与 RoleCache 平级的 MapCache，统一创建、重置及释放。WorldSyncCache 完整检查世界帧中的身份、地图规格、对象配置及远端角色后原子提交。序号缺口暂停增量，Session.ResyncWorldIfNeeded 只入队一次重同步，合法全量后恢复。旧会话由 generation 隔离。

当前首次访问会加载该地图全部动态对象，尚无服务器内存卸载、传送、怪物行为或表现。

## 背包到地面的数量丢弃

BagControl 拿起单件后点击地图直接丢弃，多件打开数量确认，默认整叠。确认前校验当前拿起上下文；关闭、刷新、切角使其失效。Session.DropItem 发送实例、数量、销毁标记及可选成对落点，不传身份和预期版本。地图校验权威位置、边界及身体避让区。

Role 完整校验最新背包候选，再通过 World 创建 GUID 地面对象，成功提交内存。本人先收到完整物品通知，观察者在世界帧收到对象增量。部分丢弃保留源编号，全量移除，编号不复用；重复命令按最新持有数量独立校验。

所有物品变更和保存共用邮箱外的单实例信号量。RepoItemBatch 捕获已变化的角色三表、账号摘要和地图，使用 items.pending.json 保存原文件字节，全部替换后删除记录；旧地图块整批完成后清理。

未完成批次在业务读取前恢复旧文件，失败保留记录与待写状态供重试；保存成功只接受捕获基线，新移动继续待写。单文件替换的持久性边界不变，异常退出可能丢失未落盘变化，但不会只恢复物品的一侧。

WorldManager 以服务器 GUID 映射当前世界的整数 entity ID；AOI 中的地面物品和怪物均创建实体，更新保持本地 ID，离开 AOI 或世界时移除。GroundItemsView 只读取 ECS 对象状态并按 GUID 复用图标、轮廓和锚点节点，不拥有 UI 控件；同点物品仅错开图标，逻辑坐标不变。地面名称与信息框由 UI 的 GroundItemOverlayControl 持有。物品资源由共享 ItemAssetService 持有，UI 与 WorldView 共同使用；地面轮廓资源只属于 WorldView。十一个全局服务及其构造、逆序释放和协议监听约束保持。


## 地面拾取与轮廓反馈

GroundItemsView 提供图标命中与视口锚点查询，UI 负责遮挡过滤、悬停名称和信息框点击。MainController 在未消费的左键点击中先查询物品；命中时调用 World.PickupItem 并消费，不同时产生普通点地涟漪。BagControl 的拿起/丢弃先于父节点处理，保留原优先级。World 从 ECS 取得物品位置和版本，走原有 MoveAction/Move 路径，再由 Session 发送绑定同一移动序号的 PickupItem。服务构造、十一个服务数量、逆序释放和协议监听约束不变。

PickupItem 只携带对象 GUID 和 move_action_id；客户端先强制发送走近 Move，再提交拾取。服务器立即回复接纳，权威时钟在一瓦片距离内按最新容量整份转移，最终用 PickupItemResultNotice.success 通知成败。新有效移动、切角、离开、断线和接管静默取消；仅背包变化不取消。

ItemRules.Add 统一 GM 和拾取的入包算法：先补堆叠，再分配空格和新编号，容量不足整份拒绝。成功后完整物品通知更新本人，世界帧移除地面对象，PickupItemResultNotice 报告最终成败；共同落盘沿用 RepoItemBatch。

World.Views.DataItemOutline 将显示图标统一到透明 32×32 画布，按非透明像素生成两像素膨胀外轮廓，资源按素材键缓存；缺图问号也使用同一图形/轮廓遮罩。地面使用 GroundAssetId 显式绑定 item/item，背包继续使用 IconAssetId 的 item/itempic；七件装备的地面键来自导出表 icon_small_code，不隐式换算编号。命中使用 32×32 显示矩形向外扩展 8 像素的 48×48 区域，含透明角落；名称由 UI 在悬停时显示，与背包共用 ItemNamePopup 的即时文本、鼠标偏移和视口裁剪，不参与命中；坐标包含摄像机变换并在静止鼠标下逐帧重新计算。UI 悬停和模态窗口阻断命中，失焦、移出、AOI 移除和离开世界清除轮廓。PointerCursor 读取表现组的悬停状态，以 HUD 调整、窗口拖动和聊天缩放、物品 Fetch、普通指针为优先序；复用 Fetch 开手/握手两帧、1.25 缩放及源热点 (6,6)，世界不反向依赖 UI。

角色与地面物品处在相同 Z 层，世界表现根和物品组启用 Y 排序，以实体等轴投影后的坐标确定前后。多件同点物品只错开图标和轮廓，物品节点仍在权威坐标，命中框随图标移动。地面不常驻显示数量；角色快捷键默认 Z 切换当前 AOI 物品的 UI 层信息框，单件只显示名称，多件显示名称及数量。信息框使用 16 号字和紧凑内边距，按屏幕位置优先沿物品中心线小间距堆叠，空间不足时再横向避让，不能无重叠放入视口时隐藏；高于世界绘制，不被角色和物品遮挡，悬停高亮并联动物品轮廓且不改变框的尺寸，点击复用现有拾取入口，不触发地图点击。GM 面板的物体锚点显示开关由 EnvService 保存到 `user://.env` 的 `RPG_SHOW_ENTITY_ANCHORS`，未设置或无效时默认关闭；重新进入世界和重启均恢复显示状态，保存失败恢复控件并提示。颜色仍只控制当前世界表现，默认黄色。角色与当前 AOI 物品在实体原点绘制标记，不参与输入，也不写入设置。

## Ctrl 销毁确认

BagControl 在拿起物品后 Ctrl 点击地图时冻结销毁意图，复用数量窗，标题、按钮及说明明确永久销毁、不掉落地面；单件仍须确认。松开 Ctrl 不改变窗口语义，取消、关闭、刷新及离开界面清理上下文。普通单件丢弃和多件数量确认保持原行为。

DropItem 的 destroy 字段默认 false；Session 仅发送意图。MockServer 复用身份、实例、数量及背包版本校验，销毁只保存候选背包并发送 RoleItemsChangedNotice 与空 ObjectId 的成功响应，不创建对象、AOI 通知或新的转移关系。背包沿用周期/关闭落地与失败重试；已有丢弃或拾取共同提交组自动包含最新背包，因此同周期转移后销毁也能一致恢复。

## 角色界面偏好

系统菜单将系统设置（显示、声音）与角色设置（界面、快捷键）分开。原 HUD 控制完整归入界面页签，继续使用独立草稿。角色、道具与地图窗口通过标题栏拖动；WindowDragControl 处理持续抓取、失焦提交及边界裁剪，松开后由 SettingService 保存相对视口坐标。窗口位置独立于 HUD 默认布局，暂时缩小视口不覆盖保存坐标。

快捷键按角色保存为逻辑键及 Ctrl/Alt/Shift 组合，默认角色 C、道具 B、地图 M、地面物品信息 Z。范围限定字母、数字、F1–F12，拒绝系统保留组合与重复绑定。录入在 _Input 消费键盘，非法输入保持高亮；合法保存后生效。普通切换在 _UnhandledKeyInput 处理，文本、模态与 HUD 调整优先。关闭角色设置或切页取消录入，界面清理不访问已撤销的角色数据。

地面丢弃由客户端 WorldService 根据当前 ECS/AOI 地面物品在角色周围的等轴画面半径 48 至 56 像素之间选择落点，排除横向小于 40 且纵向偏移小于 24 的身体遮挡区，优先与快照中已有物品保持 40 像素投影间距；拥挤时取最疏合法候选。64 个随机候选外加四个固定边缘方向确保正常地图角落有候选；评分使用预计算投影与平方距离，循环中不做除法、开方或 LINQ。DropItem 可选携带逻辑坐标；服务端仅校验它与权威角色的距离、身体避让及地图边界，不扫描物品。旧请求及极小地图无合法候选时沿用服务端原有选点和裁剪退路。快照延迟与同时丢弃可能造成重叠，间距只由当前客户端视图尽力保证。已有地面坐标不迁移，拾取继续沿用走近机制。


## 角色装备与窗口

所有角色共用十一槽位，新角色初始装备为空。服务端在不可变 DataItem 上保存 Equipped、EquipmentSlot、SlotIndex，DataEquipment 以 InstanceId 关联。实例编号由角色 NextInstanceId 分配，物品规则构造并校验完整候选，按最新状态执行。存档由专用共同批次恢复，无物品预期版本。

`BagPb` 只含全部私有 ItemPb，无 reserved 或 revision。公开 `RoleWorldEquipmentPb` 只含 repeated int32 equipped_items，身份由外层根节点携带，元素为 item.json 的配置 ID，不含实例编号、槽位、背包容量、格子或私有角色字段。它在 `RoleWorldStatePb` 中与资料、生命魔力、位置并列，可独立从配置分类推导展示。世界帧装备增量承载角色身份和公开装备，独立公开缓存实现多人可见与人物生命周期。选角页同样从 RoleSummaryPb.equipped_items 绘制双手，返回选角从已校验缓存刷新摘要，重新登录从磁盘快照读取。

选角在 state.equipment 返回装备配置编号，并列返回私有 role 与 bag；所有本人物品变更使用 RoleItemsChangedNotice，并列携带同次提交的完整私有物品和公开展示，仅发本人。RoleCache 完整校验身份、实例/格子唯一性和已穿配置 ID 一致性后原子接纳；旧代次消息忽略，非法数据报错且保留两侧旧缓存。展示内容未变化时世界跳过重复装备同步。本人装备只从选角和私有组合通知更新；公开独立/完整世界推送不能绕过原子路径，世界帧按 sequence 连续接纳。所有 PbData 转换输出独立协议对象，世界 ToPb 包含装备而排除背包及私有 Role。

`EquipItem/EquipItemRt` 位于 bag.proto，只携带实例、目标槽及可选卸下格，当前角色由连接确定。服务端按最新状态校验完整候选，失败保持物品和编号。替换回来源格，双手冲突自动收回另一手，空间不足整份失败。

角色窗口上方由 `CharacterEquipmentControl` 复用 `RolePreviewControl` 展示当前双手并保留旋转朝向，使用 1 倍原始地图比例且隐藏独立头像；选角和创角使用 2 倍比例、32 像素底部定位留白与独立头像，保证八方向脚部完整并与旋转按钮至少相隔 12 个逻辑像素，三处共用 28×28 旋转按钮和居中的 18×18 上弯弧形箭头。左侧依次为头、身体、手、裤子、鞋，右侧依次为面、项链、戒指一、戒指二，主副手在人物下方并排，与鞋子第五行对齐；预览与旋转区位于前四行。主副手可操作，其余占位；空槽内显示灰色部位名，装备后显示图标，不显示槽外标题和穿脱说明。背包控件统一持有跨窗口拿起状态，普通左键拿起、再次按下放置，点击已穿装备时自动打开背包；Ctrl＋左键点击背包装备按推荐槽自动穿戴替换，武器始终进入主手，盾牌进入副手；连续穿戴拳刃也只替换主手。分类和双手规则不按测试道具编号分支，普通手动放槽同样受服务器约束。点击已穿槽位自动卸至首个空格，右键不卸下。快捷操作先取消拿起状态，再基于当前快照发请求；界面明确反馈错误，刷新、关闭、切角使上下文失效，拿起装备后点击地图只取消，不直接丢弃。窗口继续复用拖动与小视口滚动机制。

角色窗口不再使用固定最小宽度，外框按槽位与资料的自然宽度收紧。ItemSlotStyle 统一背包和装备的 48×48 外框、46×46 内容区、4 像素行距及图标等比居中/最近邻规则；装备不创建数量标签。PopupPanelStyle.CreateTitle 为背包、角色、大地图、GM、系统与角色设置、HUD 调整和图鉴统一创建 16 字号标题。

装备和背包拿起图像统一由 `ItemSlotStyle.CreateContent` 和 `BagControl.BeginPickup` 生成，46×46 内容区、最近邻、0.85 透明度及数量展示保持背包规则。装备槽与背包槽共用 `BagControl.ShowItemInfo` 和 `ItemNamePopup` 即时信息入口，目前只显示配置名称，为后续分类信息保留统一扩展位置；不使用引擎延迟 Tooltip。装备悬停不要求背包打开；所属窗口隐藏、空槽、移出、拿起、刷新和切角清理信息。

物品素材由 `GameServices.Items` 在主线程惰性读取 `assets/items/catalog.json` 并建立只读索引。背包、装备窗口、地面物品和图鉴共同访问该服务，不各自解析目录；图集按路径、裁剪纹理按素材键缓存并强引用。选角成功后、世界与 HUD 创建前仅预热当前背包和装备的图标；空包不加载无关图集，切角继续复用静态资源，新增物品按需补充。完整服务释放时清空缓存，仍由节点持有的纹理由 Godot 管理。首次目录解析和所需图集加载仍在进入阶段同步执行。

完整逐帧审查使用 `tools/export_actor_review.ps1` 调用独立开发场景，直接渲染正式ActorView。按当前配置允许的主副手组合、男女13动作和八方向导出每个身体帧，固定默认头型1，统一画布与定位点；保留当前武器动作复用与遮挡。Pillow整理透明PNG、中文联系表、离线浏览索引、覆盖与采样清单，并校验像素和ZIP。导出目录带 `.gdignore`，图片和ZIP只放忽略的build；工具不修改正式资源与存档。使用方法见 `docs/actor-review-export.md`。

拳刃和暗器保持一件主手装备实例与空副手槽，`ActorView` 按主手分类派生源左手图像；左右分别使用已导入的原生变体和挂点，整体镜像。替换或卸下主手时同步清理两手图像，世界与角色预览共用同一呈现逻辑。逐帧导出只枚举9种合法槽位组合，清单保留实际main/off装备编号，samples.off记录派生左手的实际资源。

## 登录与真实网络托管

角色编号由登录存储内共享的 RoleIdGenerator 分配，与账号和连接模式无关；全新目录从 1003 开始。RepoRoleIdSequence 独占预留并先持久化最多 256 个编号的上界，生成器异步互斥发放，重启跳过未使用预留段，删除及未落地摘要不会归还编号。LoginActor 在校验创建意图后准备当次编号，Handler 只提交内存，结束回合清除暂存编号；损坏、IO 失败和耗尽拒绝创建。编号上界与服务器角色存档共同保存，旧账号区间数据不兼容，不提供独立目录间的跨服唯一性。

Net 默认使用 TCP，借用 GameServices 已装配的非空 MockServerService；GameServices 统一在 Net 之后释放该服务。登录选择服务器模式时才创建独立 MockServer，监听成功后转移所有权；纯客户端只连接指定 IP/端口。本机玩家也使用回环 TCP。MockServer 自带后台时钟，服务器生命周期不依赖客户端固定帧；返回登录保持服务器，退出或确认配置切换后有序停止。客户端与回归使用同一正式实现。

四字段信封不变，会话代次隔离由传输和运行时负责；客户端业务事件仍固定帧派发。本地写回以后台响应为屏障，不新增保存网络消息。
