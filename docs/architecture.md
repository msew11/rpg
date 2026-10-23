# 客户端架构

## 数据和生命周期

`RepoGameConfig` 在启动时递归扫描 `res://config`，以 `res://schemas` 中的 Draft 2020-12 JSON Schema 校验，并独占持有已加载的配置。首期 `config/maps/map.json` 对应 `Rpg.Config.Data.MapConfig`，包含地图尺寸、瓦片规格、默认地块及稀疏覆盖格；地图管理器与 UI 持有配置仓库，需要地图数据时从仓库读取，不另存 `MapConfig` 引用。文件名与配置类型不符、未知、重复或无效文件均阻止启动。配置与程序同包发布。

`NetManager` 在登录页和角色世界都由主控制器固定帧推进，`Send(IMessage)` 在调用时序列化并入队，回调中新请求留到下一帧发送。`INetTransport` 隔离具体传输，当前 `MockTransport` 在同进程内连接 `MockServer`，双向只交换 protobuf 字节，不传递共享对象。真实连接以后实现新的传输适配器，世界与 UI 不接触传输细节。

`GameSession` 在选角请求前建立 `DataCache`，后者组合功能缓存，本期只有 `RoleCache`。`NetManager.Events` 是位于 Session 模块的实例化 `EventSystem`：具体类型监听返回 `IDisposable` 句柄，固定先执行 `EventPhase.Cache` 再执行普通监听，各阶段按注册顺序。`RoleCache` 监听 `SelectRoleRt`、`MoveRt`、`RoleStateNotice`，将协议对象转换为客户端不可变快照，按角色 ID 与 revision 过滤。选角响应先初始化缓存，再创建世界与 HUD；普通监听读取缓存并向 ECS 排入同步 Action。

事件分发采用单条消息的监听快照：新增监听处理后续消息，已取消监听立即跳过，嵌套发布排队。缓存监听失败报告错误并跳过该消息的普通阶段；普通监听失败不阻止其他监听。网络及监听错误通过 `NetManager.Error` 报告并显示在登录/选角页或 HUD。角色会话结束立即释放缓存和 HUD 监听；网络重置递增信封代次、清理队列及服务端未确认移动，旧消息不能进入新会话。常驻登录监听保留到主控制器释放。

本地设置与上述两类数据分离：系统设置保存在 `user://settings/system.json`；角色设置按 `role_id` 保存在 `user://settings/roles/`。`RepoSettings` 持有当前系统和选中角色设置，集中修改与保存；选角时加载对应角色设置。UI 只读取当前值并请求修改，窗口、声音和 HUD 的实际呈现仍由 UI 应用。设置仅控制本机显示、声音和 HUD 布局，不作为服务器权威数据。

## 协议与模拟服务端

根目录 `proto/game.proto` 声明消息，请求无后缀、响应以 `Rt` 结尾、推送以 `Notice` 结尾。现有消息为 `Login/LoginRt`、`SelectRole/SelectRoleRt`、`Move/MoveRt`、`RoleStateNotice`。信封包含协议完整名称、会话代次与 payload；`MessageCodec` 显式注册并校验传输方向，未知或损坏消息报告错误而不分发。增加业务消息时同时更新 proto 和协议表，并在对应功能缓存注册处理方法。

`dotnet build rpg.csproj` 使用仅构建依赖 `Grpc.Tools 2.84.0` 自动生成 `src/Net/Message/Game.cs`，运行依赖为 `Google.Protobuf 3.36.2`，未启用 gRPC。生成 C# 与 proto 一起提交，中文协议注释写在 proto，不直接修改生成文件。SDK 排除生成目录的默认编译项，再由 protobuf 构建目标添加，避免重复编译。

`src/Server` 的 `MockServer` 使用 `src/Server/Data` 下的独立 `DataServerRole` 和服务端性别枚举，不引用客户端模型或 Godot。`RepoServerRole` 按角色 ID 保存到 `user://server/<roleId>.json`；服务端目录可在入口注入，测试统一使用临时目录。首次缺少文件时创建既有演示角色；损坏数据、身份不符或写入失败明确报错，不静默覆盖。保存先写同目录临时文件并刷盘，再替换目标文件，成功后才提交内存状态并返回响应或 Notice。

模拟移动保留每秒 180 逻辑单位、0.45 秒确认及横向 3 单位校正偏差。仅保存已确认/推送状态，不保存会话结束时仍待确认的移动；选角恢复位置和 revision，移动确认序号重置为 0，供新世界使用。`PublishRoleState` 提供服务端主动更新入口，保存成功后发送 Notice，不添加产品操作面板。JSON 存储仅面向单实例进程内 Mock，不提供多进程写入协调。

普通消息监听示例（缓存模块使用相同 API 并指定 `EventPhase.Cache`）：

```csharp
using var subscription = session.Events.Subscribe<MoveRt>(response =>
{
    if (response.Success)
        world.Enqueue(new SyncRoleAction(session.Cache.Role.State));
});
net.Send(new Move { RoleId = session.RoleId, ActionId = 1, X = 16048, Y = 16016 });
```

## 输入、世界与表现

独立的 `InputSystem` 把未被 UI 消费的 Godot 输入转换为世界 Action，也为今后的 UI 输入路由留出位置。`WorldManager` 是 ECS 的唯一外部写入入口：输入 Action、缓存状态同步 Action 排队后由 Godot 固定帧处理。初始化顺序为地图世界实体、角色实体。轻量 ECS 由整数实体 ID、组件仓库和固定顺序系统组成；首期移动系统按直线推进并接受权威位置修正。

`MapManager` 内聚地图逻辑坐标、边界与可见格计算。`IsometricProjection` 在世界表现与地图点击边界做双向转换，ECS、配置、雷达和大地图继续使用逻辑坐标。地图表现将视口附近地块划为 8×8 格分块，各分块一次绘制 2:1 等轴菱形并复用画布命令；相机或视口跨越分块范围时只增删差异分块，不创建百万个瓦片节点。角色与摄像机使用同一投影。HUD、小地图、GM 和系统面板放在独立 `CanvasLayer`，不受世界摄像机变换影响，从 `RepoGameConfig`、`DataCache` 与本地设置读取各自所需的数据。GM 面板通过 `RepoSettings` 获取系统和当前角色设置文件路径，并交由桌面文件管理器定位。

角色数据的 `RoleGender` 随选角与会话缓存进入玩家 entity。`DataRoleComponent` 保存角色 ID 和性别，`DataActorComponent` 保存动作语义、逻辑朝向与显式动作请求版本；`MovementSystem` 根据实际移动切换跑步、到达后站立并保留朝向，`SetRoleMotionAction` 可请求其他基础身体动作并停止本地移动。角色属性同步按权威 revision 接纳，位置修正继续按移动确认序号过滤。`WorldManager.ActorState` 只提供不可变快照，不暴露可写组件。

`ActorView.Present` 读取快照并投影位置和朝向，表现层的 `RepoActorAnimation` 按性别和动作缓存 `assets/characters` 下的 `DataActorAnimation` 原生资源。每个动作包含五方向 `SpriteFrames` 与原始定位点；节点围绕定位点镜像出其余三个方向。`AnimatedSprite2D` 独立推进显示时间；位置刷新不重启动画，改向保留帧进度。站立和跑步循环，其他动作播放一次后保持末帧；播放结束不改变世界逻辑。现阶段仅接入身体 13 个动作，不附加攻击、施法或骑乘效果。图集可通过 `tools/import_actor_animations.ps1` 从外部解码清单重建，项目运行与导出不依赖外部素材目录。

```text
map.json -> RepoGameConfig.MapConfig -> 地图管理 / UI
NetManager.Send -> protobuf 字节 -> MockServer -> JSON 落地
MockServer -> protobuf 字节 -> EventSystem(Cache) -> DataCache.Role
                                      -> EventSystem(Normal) -> UI / 同步 Action -> WorldManager -> ECS -> 世界表现
Godot 输入 -> 等轴逆投影 -> InputSystem -> 移动 Action --------^
本地设置 -> RepoSettings（当前系统与角色设置）-----------> UI
```

## 当前限制

演示认证、状态确认和地图配色仅用于验证客户端链路；尚无真实网络、寻路、技能效果或背包操作。角色目前仅接入基础身体素材，不包含独立头部、发型、装备和坐骑；正式美术方向仍待确认。动作播放暂用每秒 10 帧，不代表玩法节奏。模拟服务端位置与客户端运动存在固定小偏差，用于检查校正路径。
