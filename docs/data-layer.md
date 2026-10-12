# 数据仓库框架

`scripts/core/data/` 定义 `IDataRepoData`、`IDataRepo`、`DataRepo<T>`、`DataRepoSingle<T>`、`DataRepoMultiple<T>`、`DataRepoStorageKind` 和 `DataRepoCache`。仓库的泛型参数 `T` 必须实现 `IDataRepoData`；`GetDataType()` 返回 `typeof(T)`。具体仓库可继承 `DataRepoSingle<T>` 或 `DataRepoMultiple<T>`，分别固定 `GetStorageKind()` 为 `Single` 或 `Multiple`，下级仓库不能改写。除单例仓库的数据入口外，框架尚不规定读取、集合标识、写入或持久化行为。

`DataRepoSingle<T>` 的具体仓库须提供 `Data` 属性，返回它承载的唯一数据实例；其他数据操作仍由具体功能设计。

`DataRepoCache` 是继承 Godot `RefCounted` 的抽象管理入口，按仓库 C# `Type` 返回 `IDataRepo`，无需进入场景树。核心层不提供具体仓库、缓存实现或 AutoLoad 配置。

核心只提供 `IEcsWorldData` 状态访问接口。`scripts/data/EcsWorldData.cs` 是本地 ECS 实体状态实现，包含实体 ID 分配状态、有效实体集合与组件索引；存档收集方式仍由后续功能确定。

`develop` 中的 `LocalDataRepoCache` 由每个 `LocalSession` 创建并持有，不进入场景树。缓存初始化时便创建唯一的 `EcsWorldDataRepo : DataRepoSingle<EcsWorldData>` 及其数据，按 `typeof(EcsWorldDataRepo)` 返回仓库。`WorldState` 构造时接收缓存，把仓库的数据交给 `EcsWorld`。GDScript 通过 `WorldState.Create(cache)` 调用 C# 的 `new WorldState(cache)`；Godot 的 `.new()` 不接受这个 C# 构造参数。缓存还绑定当前会话的 `WorkingSaveStore`，只通过该临时存档加载和落地区块变化；正式槽位由会话调用者选择和提交。

后续具体仓库和数据类一般分别采用 `Repo`、`Data` 前缀，仓库名再用用途段和数据内容段区分职责，例如 `RepoPlayerItem`、`DataPlayerItem`；已确定的 ECS 仓库专名为 `EcsWorldDataRepo`。示例名称尚未实现，存档操作留待后续 change 确定。
