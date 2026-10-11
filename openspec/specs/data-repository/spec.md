# data-repository Specification

## Purpose

定义所有数据仓库及其统一管理入口的上层契约，为后续按用途和数据类型实现仓库提供稳定边界。
## Requirements
### Requirement: 仓库抽象

核心框架 SHALL 提供 C# `DataRepo<T>` 抽象基类、`DataRepoSingle<T>` 与 `DataRepoMultiple<T>` 两种形态抽象基类，以及 `IDataRepoData` 接口；三个泛型基类的 `T` SHALL 在编译期受 `IDataRepoData` 约束。`DataRepoSingle<T>` 的 `GetStorageKind()` SHALL 固定返回 `Single`，并 SHALL 要求具体仓库提供唯一数据实例的 `Data` 属性；`DataRepoMultiple<T>` 的 `GetStorageKind()` SHALL 固定返回 `Multiple`，且下级仓库 SHALL 不能覆写这两个返回值。`GetDataType()` SHALL 返回 `typeof(T)`。核心框架 SHALL 提供 `DataEcsWorld` 作为 ECS 状态数据类型，但 SHALL 不实现具体仓库。

#### Scenario: 单例仓库设计

- **WHEN** 后续功能设计只承载一个实现 `IDataRepoData` 的数据类型的仓库
- **THEN** 该仓库能够继承 `DataRepoSingle<T>`，提供唯一数据实例，并通过 `IDataRepo` 获得该数据类型和单例形态

#### Scenario: 多例仓库设计

- **WHEN** 后续功能设计承载一组同类型数据实例的仓库
- **THEN** 该仓库能够继承 `DataRepoMultiple<T>`，通过 `IDataRepo` 获得该数据类型和多例形态

#### Scenario: 无效数据类型

- **WHEN** 仓库尝试将未实现 `IDataRepoData` 的类型作为 `T`
- **THEN** C# 编译失败

### Requirement: 管理入口抽象
核心框架 SHALL 提供继承 `RefCounted` 的 C# `DataRepoCache` 抽象基类，定义按仓库 `Type` 获取 `IDataRepo` 的统一入口。该类 SHALL 不要求加入场景树。核心框架 SHALL 不实例化该类，也 SHALL 不注册 AutoLoad 单例。

#### Scenario: 会话私有管理器
- **WHEN** 后续功能实现由 GDScript 会话持有的统一仓库管理器
- **THEN** 它能够继承 `DataRepoCache` 并提供具体仓库查找行为，无须作为场景节点使用

### Requirement: 命名边界
后续仓库及其承载的数据类 SHALL 一般分别采用 `Repo` 和 `Data` 前缀；ECS 世界仓库采用已确定的专名 `DataRepoEcsWorld`。仓库名称 SHALL 使用用途段和数据内容段区分职责。除 `DataEcsWorld` 外，核心框架 SHALL 不预先声明功能仓库或数据类型。

#### Scenario: 具体仓库留待后续
- **WHEN** 检查本次核心框架
- **THEN** 包含数据仓库上层抽象及 `DataEcsWorld`，不包含具体仓库
