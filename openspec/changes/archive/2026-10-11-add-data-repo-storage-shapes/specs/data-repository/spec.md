## MODIFIED Requirements

### Requirement: 仓库抽象

核心框架 SHALL 提供 C# `DataRepo<T>` 抽象基类、`DataRepoSingle<T>` 与 `DataRepoMultiple<T>` 两种形态抽象基类，以及 `IDataRepoData` 接口；三个泛型基类的 `T` SHALL 在编译期受 `IDataRepoData` 约束。`DataRepoSingle<T>` 的 `GetStorageKind()` SHALL 固定返回 `Single`，`DataRepoMultiple<T>` 的 `GetStorageKind()` SHALL 固定返回 `Multiple`，且下级仓库 SHALL 不能覆写这两个返回值。`GetDataType()` SHALL 返回 `typeof(T)`。核心框架 SHALL 不实现任何具体仓库或数据类。

#### Scenario: 单例仓库设计

- **WHEN** 后续功能设计只承载一个实现 `IDataRepoData` 的数据类型的仓库
- **THEN** 该仓库能够继承 `DataRepoSingle<T>`，通过 `IDataRepo` 获得该数据类型和单例形态

#### Scenario: 多例仓库设计

- **WHEN** 后续功能设计承载一组同类型数据实例的仓库
- **THEN** 该仓库能够继承 `DataRepoMultiple<T>`，通过 `IDataRepo` 获得该数据类型和多例形态

#### Scenario: 无效数据类型

- **WHEN** 仓库尝试将未实现 `IDataRepoData` 的类型作为 `T`
- **THEN** C# 编译失败
