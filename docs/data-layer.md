# 数据仓库框架

`scripts/core/data/` 定义 `IDataRepoData`、`IDataRepo`、`DataRepo<T>`、`DataRepoSingle<T>`、`DataRepoMultiple<T>`、`DataRepoStorageKind` 和 `DataRepoCache`。仓库的泛型参数 `T` 必须实现 `IDataRepoData`；`GetDataType()` 返回 `typeof(T)`。具体仓库可继承 `DataRepoSingle<T>` 或 `DataRepoMultiple<T>`，分别固定 `GetStorageKind()` 为 `Single` 或 `Multiple`，下级仓库不能改写。框架尚不规定读取、集合标识、写入或持久化行为。

`DataRepoCache` 是继承 Godot `RefCounted` 的抽象管理入口，按仓库 C# `Type` 返回 `IDataRepo`，无需进入场景树。目前没有具体仓库、数据类、缓存实现或 AutoLoad 配置。

后续具体仓库和数据类分别采用 `Repo`、`Data` 前缀，仓库名再用用途段和数据内容段区分职责，例如 `RepoPlayerItem`、`DataPlayerItem`。示例只说明命名结构，并未实现这些类型。具体管理器、对象生命周期和数据操作由 `develop` 后续独立 change 确定。
