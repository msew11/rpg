# 客户端架构

## 服务、数据和生命周期

客户端共享业务枚举放在 `src/Const/`，使用 `Rpg.Const` 命名空间，例如 `RoleGender` 和 `RoleMotion`。

`IService` 位于 `src/Services/`，统一约定服务释放。`GameServices` 是主线程的固定服务集合，提供只读 `Config`、`Setting`、`Net`、`Session`、`World`、`Animation` 入口。参考 Dalamud 的服务契约、构造依赖与逆依赖卸载原则；当前服务数量固定，通过显式组合表达依赖。服务之间借用构造参数传入的同次启动实例，Godot 节点通过全局入口调用业务，不缓存服务、账号缓存或世界管理器。节点只拥有 Godot 节点、订阅句柄、界面布局和上次呈现的不可变快照。

| 服务 | 拥有的状态和行为 | 内部依赖 |
| --- | --- | --- |
| `ConfigService Config` | 构造时加载、校验并持有配置 | 内部 JSON 与 Schema 读取方法 |
| `SettingService Setting` | 构造时加载系统设置，内部持有角色设置并修改、保存 | 私有 JSON 读写方法 |
| `EventService Events` | 唯一类型事件域、缓存优先分发、监听错误与订阅生命周期 | 内部监听集合与重入队列 |
| `NetService Net` | protobuf 请求队列、固定帧收发及解码后发布消息 | `INetTransport`；通过 GameServices.Events 发布 |
| `SessionService Session` | 直接建立并持有账号摘要与角色缓存，处理登录、选角和快照 | `NetService`、内部 `LoginCache` 与 `RoleCache` |
| `WorldService World` | 地图、ECS、输入转换、角色初始化与权威同步 | Config、Setting、Session；内部 `MapManager`、`WorldManager` |
| `AnimationService Animation` | 直接加载、缓存身体动画并解析朝向 | Godot 原生资源 |

`ConfigService` 构造时递归扫描 `res://config`，以 `res://schemas` 中的 Draft 2020-12 JSON Schema 校验并持有 `MapConfig`。地图逻辑、分块和 UI 通过服务读取，不另存配置引用。`config/maps/map.json` 对应 `Rpg.Config.Data.MapConfig`，包含地图尺寸、瓦片规格、默认地块与稀疏覆盖格。未知、重复、无效或文件名不匹配的配置阻止启动；配置与程序同包发布。原始加载方法为私有，独立目录构造供工具与隔离测试使用。

主控制器调用 `GameServices.Initialize(settingsDirectory, serverDirectory, createNet)`。GameServices 内部创建配置、设置、唯一事件、网络、会话、世界和动画服务；`NetService` 自行建立当前阶段的模拟传输，`SessionService` 自行建立账号缓存。初始化返回所有权句柄；注册前访问、已释放实例注册和重复初始化明确报错。失败时回收已创建服务，释放时先撤销业务入口，再按 Animation、World、Session、Net、Setting、Config、Events 顺序清理并撤销事件入口；单项失败仍继续清理其他服务，最后报告异常。旧句柄重复释放不影响新注册。内部保留显式实例注册入口供测试使用，业务节点不负责组装或释放单个服务。

`MainController` 只协调页面与表现：登录和选角按钮调用 Session 服务，成功后创建相应页面或世界表现；固定帧先推进 Net 再推进 World，再更新角色与摄像机。输入由节点完成视口逆投影，再交给 World 服务限制边界、分配移动序号并同时提交 ECS Action 与网络请求。`CreateNetService` 保留场景测试替换传输的边界，设置与服务端目录可在启动前注入。启动失败和退出共用清理，先撤销 UI 监听、提交拖动设置，再释放服务句柄；未获得句柄的控制器不会释放其他启动流程的服务。

`SessionService.Login` 在请求前清空旧账号并隔离网络代次。成功 `LoginRt` 在缓存阶段直接建立并持有 `LoginCache` 与 `RoleCache`，不再经过独立的账号包装类；此路径不重置正在分发的成功响应。非法摘要不留下账号状态或订阅，并阻止 UI 成功回调。UI 只读取 `GameServices.Session.Roles`、`RoleId`、`Role` 和状态标记；缓存容器与已有响应构造均为内部接口。服务绑定创建时的网络及账号代次，旧实例不能操作后来注册的网络或账号。

选角复用账号会话，发送请求前清空角色缓存并隔离旧代次。`RoleCache` 成功初始化后，World 服务先建立地图实体，再建立角色实体，并选择对应角色设置；随后 UI 创建表现。`WorldService` 监听移动响应与主动推送，将当前缓存快照转为同步 Action，不允许节点直接写 ECS。选角失败保留账号摘要并恢复角色列表。返回选角清理世界、角色数据和当前角色设置，保留同一个账号；返回登录由 Session 服务释放账号缓存并清空账号状态，各服务仍常驻到场景结束。后续背包数据计划以 `BagPbData` 归入 `RoleCache`，当前没有 `DataCache`、独立 `BagCache` 或背包协议。

所有功能缓存位于 `src/Session/Cache/`，继承 `CacheBase`，通过受保护的 `Subscribe<T>()` 注册缓存阶段监听，由基类私有集合托管句柄。释放幂等取消自身订阅并执行一次 `OnDispose()`，释放后禁止新增订阅。所有 `PbData` 类位于 `src/Session/Data/`，通过自身 `FromPb` 与 `ToPb` 校验、复制数据，不共享可变协议对象。`RoleWorldStatePbData` 直接持有角色属性、性别、逻辑位置、revision 和移动确认序号，不嵌套额外角色模型；World 与 UI 使用相同的不可变权威快照。

`GameServices.Events` 是独立的 `EventService : IService`，直接包含分发实现，没有额外转发包装。构造方法不对外公开，GameServices 是唯一创建位置；网络、会话、缓存、世界与 UI 统一通过该入口使用，不持有私有事件域，也不逐层传递事件对象。独立网络保留各自发送队列，但共享该事件服务；单独释放网络不清除事件监听，整体退出最后释放事件服务并清除全部监听和队列。分发规则：先 Cache 后 Normal，同阶段按注册顺序，每条消息使用监听快照。新增监听处理后续消息，已取消监听立即跳过，嵌套发布排队。缓存失败跳过该消息的普通阶段，普通监听失败不阻止其他监听。Session 服务在 Cache 阶段建立账号，World 服务先于 UI 注册 Normal 监听。网络编解码与传输错误通过 `NetService.Error`、监听错误通过 `GameServices.Events.Error` 报告至页面或 HUD。会话重置递增信封代次、清理队列和服务端未确认移动，旧消息不能进入后续角色。

`NetService.Send(IMessage)` 调用时序列化并入队，回调内新增请求留到下一帧发送。`INetTransport` 隔离具体传输，`MockTransport` 在同进程连接 `MockServer`，双向仅交换 protobuf 字节。真实连接以后提供新的传输适配器，世界与 UI 不接触传输细节。

本地系统设置保存到 `user://settings/system.json`，角色设置按 `role_id` 保存到 `user://settings/roles/`。`SettingService` 独占当前设置，提供当前值、分辨率、音量、技能栏和 HUD 布局操作；原始读写方法保持私有。离开角色或关闭服务时提交未保存布局并清空角色设置。窗口、声音和 HUD 实际呈现由节点应用，设置不作为服务器权威数据。

## 协议与模拟服务端

`proto/` 按业务声明消息：`message.proto` 定义公共信封，`role.proto` 定义角色共享枚举（当前为 `RoleGenderPb`），`login.proto` 定义登录、角色摘要和选角请求/响应，`world.proto` 定义角色世界状态、移动请求/响应及状态推送。请求无后缀、响应以 `Rt` 结尾、推送以 `Notice` 结尾，纯数据结构与枚举以 `Pb` 结尾。`LoginRt` 返回 `RoleSummaryPb` 列表，仅包含角色 ID、名称、等级与性别，客户端转换为独立的 `RoleSummaryPbData`。`SelectRoleRt` 才返回完整角色世界状态 `RoleWorldStatePb`，移动响应与状态推送也使用该结构。信封与性别枚举分别为 `EnvelopePb`、`RoleGenderPb`。现有消息为 `Login/LoginRt`、`SelectRole/SelectRoleRt`、`Move/MoveRt`、`RoleStateNotice`。信封包含协议完整名称、会话代次与 payload；`MessageCodec` 显式注册并校验传输方向，未知或损坏消息报告错误而不分发。增加业务消息时同时更新 proto 和协议表，并在对应功能缓存注册处理方法。

`dotnet build rpg.csproj` 使用仅构建依赖 `Grpc.Tools 2.84.0` 自动生成 `src/Net/Message/Message.cs`、`Role.cs`、`Login.cs` 和 `World.cs`，运行依赖为 `Google.Protobuf 3.36.2`，未启用 gRPC。生成 C# 与 proto 一起提交，中文协议注释写在 proto，不直接修改生成文件。SDK 排除生成目录的默认编译项，再由 protobuf 构建目标添加，避免重复编译。

`src/Server` 的 `MockServer` 使用 `src/Server/Data` 下的独立 `DataServerRole` 和服务端性别枚举，不引用客户端模型或 Godot。`RepoServerRole` 按角色 ID 保存到 `user://server/<roleId>.json`；服务端目录可在入口注入，测试统一使用临时目录。首次缺少文件时创建既有演示角色；损坏数据、身份不符或写入失败明确报错，不静默覆盖。保存先写同目录临时文件并刷盘，再替换目标文件，成功后才提交内存状态并返回响应或 Notice。

模拟移动保留每秒 180 逻辑单位、0.45 秒确认及横向 3 单位校正偏差。仅保存已确认/推送状态，不保存会话结束时仍待确认的移动；选角恢复位置和 revision，移动确认序号重置为 0，供新世界使用。`PublishRoleState` 提供服务端主动更新入口，保存成功后发送 Notice，不添加产品操作面板。JSON 存储仅面向单实例进程内 Mock，不提供多进程写入协调。

普通 UI 监听通过 `GameServices.Events.Subscribe<T>()` 获取释放句柄，读取 `GameServices.Session.Role`；世界同步监听留在 World 服务内部。缓存模块使用相同 API 并指定 `EventPhase.Cache`。

## 输入、世界与表现

`WorldService` 直接把未被 UI 消费的 Godot 点击转换为移动 Action，不经过单独的输入转发类。`WorldManager` 是 ECS 的唯一外部写入入口：输入 Action、缓存状态同步 Action 排队后由 Godot 固定帧处理。初始化顺序为地图世界实体、角色实体。轻量 ECS 由整数实体 ID、组件仓库和固定顺序系统组成；首期移动系统按直线推进并接受权威位置修正。

`MapManager` 内聚地图逻辑坐标、边界与可见格计算。`IsometricProjection` 在世界表现与地图点击边界做双向转换，ECS、配置、雷达和大地图继续使用逻辑坐标。地图表现将视口附近地块划为 8×8 格分块，各分块一次绘制 2:1 等轴菱形并复用画布命令；相机或视口跨越分块范围时只增删差异分块，不创建百万个瓦片节点。角色与摄像机使用同一投影。HUD、小地图、GM 和系统面板放在独立 `CanvasLayer`，不受世界摄像机变换影响，从 `GameServices.Config`、`Session` 与 `Setting` 读取所需数据。GM 面板从设置服务获取系统和当前角色设置路径，交由桌面文件管理器定位。

角色数据的 `RoleGender` 随选角与会话缓存进入玩家 entity。`DataRoleComponent` 保存角色 ID 和性别，`DataActorComponent` 保存动作语义、逻辑朝向与显式动作请求版本；`MovementSystem` 根据实际移动切换跑步、到达后站立并保留朝向，`SetRoleMotionAction` 可请求其他基础身体动作并停止本地移动。角色属性同步按权威 revision 接纳，位置修正继续按移动确认序号过滤。`WorldManager.ActorState` 只提供不可变快照，不暴露可写组件。

`ActorView.Present` 读取快照并投影位置和朝向，表现层的 `AnimationService` 直接按性别和动作缓存 `assets/characters` 下的 `DataActorAnimation` 原生资源。每个动作包含五方向 `SpriteFrames` 与原始定位点；节点围绕定位点镜像出其余三个方向。`AnimatedSprite2D` 独立推进显示时间；位置刷新不重启动画，改向保留帧进度。站立和跑步循环，其他动作播放一次后保持末帧；播放结束不改变世界逻辑。现阶段仅接入身体 13 个动作，不附加攻击、施法或骑乘效果。图集可通过 `tools/import_actor_animations.ps1` 从外部解码清单重建，项目运行与导出不依赖外部素材目录。

```text
map.json -> GameServices.Config -> World / 地图表现 / UI
UI -> GameServices.Session -> GameServices.Net -> protobuf 字节 -> MockServer -> JSON
MockServer -> protobuf 字节 -> GameServices.Events(Cache) -> Session 内部账号缓存
                           -> GameServices.Events(Normal) -> World 同步 Action -> ECS -> 只读表现快照
                                                  -> UI 刷新
Godot 输入 -> 等轴逆投影 -> GameServices.World -> 移动 Action / 会话移动请求
本地设置 <-> GameServices.Setting -> UI
动画资源 -> GameServices.Animation -> ActorView
```

## 当前限制

演示认证、状态确认和地图配色仅用于验证客户端链路；尚无真实网络、寻路、技能效果或背包操作。角色目前仅接入基础身体素材，不包含独立头部、发型、装备和坐骑；正式美术方向仍待确认。动作播放暂用每秒 10 帧，不代表玩法节奏。模拟服务端位置与客户端运动存在固定小偏差，用于检查校正路径。
