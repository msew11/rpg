# 数据仓库框架

`scripts/core/data/` 定义 `IDataRepoData`、`IDataRepo`、`DataRepo<T>`、`DataRepoSingle<T>`、`DataRepoMultiple<T>`、`DataRepoStorageKind` 和 `DataRepoCache`。仓库的泛型参数 `T` 必须实现 `IDataRepoData`；`GetDataType()` 返回 `typeof(T)`。具体仓库可继承 `DataRepoSingle<T>` 或 `DataRepoMultiple<T>`，分别固定 `GetStorageKind()` 为 `Single` 或 `Multiple`，下级仓库不能改写。除单例仓库的数据入口外，框架尚不规定读取、集合标识、写入或持久化行为。

`DataRepoSingle<T>` 的具体仓库须提供 `Data` 属性，返回它承载的唯一数据实例；其他数据操作仍由具体功能设计。

`DataRepoCache` 是继承 Godot `RefCounted` 的抽象管理入口，按仓库 C# `Type` 返回 `IDataRepo`，无需进入场景树。核心层不提供具体仓库、缓存实现或 AutoLoad 配置。

`DataEcsWorld` 是核心提供的 ECS 实体状态数据类型，包含实体 ID 分配状态、有效实体集合与组件索引；具体仓库和存档收集方式仍由后续功能确定。

`develop` 中的 `LocalDataRepoCache` 由每个 `LocalSession` 创建并持有，不进入场景树。`WorldState` 把 ECS 使用的同一 `DataEcsWorld` 绑定到 `RepoWorldEcs : DataRepoSingle<DataEcsWorld>`；缓存按 `typeof(RepoWorldEcs)` 返回仓库。该绑定只允许一次。组件读写仍通过 `EcsWorld`，仓库当前尚无存档方法。

后续具体仓库和数据类分别采用 `Repo`、`Data` 前缀，仓库名再用用途段和数据内容段区分职责，例如 `RepoPlayerItem`、`DataPlayerItem`。示例只说明命名结构，并未实现这些类型。具体管理器、对象生命周期和数据操作由 `develop` 后续独立 change 确定。
