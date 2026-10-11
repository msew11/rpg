## Context

当前核心层只有 `DataSource` 具名键值抽象，正式运行时代码尚无依赖；测试替身和契约测试是唯一使用方。用户明确要求 `main` 只保留上层抽象，具体类型化仓库和统一单例由 `develop` 实现。

## Goals / Non-Goals

**Goals:** 提供两个具有明确职责的抽象类，区分仓库承载类型与单例/多例形态，并移除旧数据源契约。

**Non-Goals:** 实现任何具体仓库、数据类、注册表、AutoLoad、存储或持久化策略。

## Decisions

- `DataRepo` 继承 `RefCounted`，使用 `@abstract` 声明类及 `get_data_type() -> Script`、`get_storage_kind() -> StorageKind`。GDScript 4.7.2 无类泛型参数，具体类日后可将类型脚本作为元数据返回，并用其自己的接口声明静态数据类型；框架不通过 `Variant` 假装提供编译期泛型。
- `StorageKind` 枚举包含 `SINGLE`、`MULTIPLE`，只描述承载形态。实例保存、集合标识及读写 API 需要具体需求确定后再实现。
- `DataRepoCache` 继承 `Node` 并保持 `@abstract`，只定义 `get_repo(repo_type: Script) -> DataRepo`。后续具体子类可成为 AutoLoad 单例。此处选择 `Node` 是为了留出该继承路径；本次不实例化或配置单例。仓库注册与生命周期由具体管理器确定。
- 删除 `DataSource`、其 UID、测试替身及旧契约测试。移除检查脚本中的旧测试入口，保留 Godot 项目导入、启动场景和 ECS 检查。用文档说明当前抽象及其边界。

## Risks / Trade-offs

- [无 GDScript 泛型] → 基类只能返回 `Script` 类型标识和 `DataRepo`；具体仓库需要自行提供静态类型化 API。
- [当前无法测试具体存储行为] → 以项目导入和脚本解析验证抽象可用；具体读写验收留给后续实现。
- [旧接口破坏性移除] → 当前仅测试使用，删除相应测试和检查入口；若其他分支后来增加调用方，应在合并时迁移。

## Migration Plan

在 `main` 完成抽象和旧接口清理，运行统一检查后归档 change 并提交。`develop` 通过合并 `main` 获取框架，再以独立 change 实现具体管理单例和仓库。

## Open Questions

具体仓库的对象标识、读写操作、缓存生命周期、持久化和自动加载名称，均由未来需求确定。
