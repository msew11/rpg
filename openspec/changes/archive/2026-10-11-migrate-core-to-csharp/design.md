## Context

`main` 仅持有核心数据仓库抽象和 ECS；`develop` 的玩法脚本继承现有 GDScript ECS 基类。Godot 不允许 GDScript 继承 C# 脚本，因此本 change 只迁移 `main` 核心，玩法调用方需在核心经 PR 同步后迁移。

## Goals / Non-Goals

**Goals:** 使用 Godot 4.7.2 .NET 版运行核心；保留 ECS 的实体、组件、查询与调度语义；仓库数据类型由 C# 泛型和接口在编译期约束；核心测试覆盖原有验收场景。

**Non-Goals:** 不新增具体数据仓库、玩法组件、持久化或 AutoLoad；不在 `main` 修改玩法脚本。

## Decisions

- 数据层使用纯 C# 的 `IDataRepoData`、`IDataRepo`、`DataRepo<T>`，其中 `T : IDataRepoData`，`GetDataType()` 返回 `typeof(T)`。`DataRepoCache` 仍继承 Godot `Node`，统一入口接受仓库的 `Type` 并返回 `IDataRepo`。泛型基类不注册为 Godot 脚本类，避免泛型脚本实例化限制。
- ECS 类继承 `RefCounted`，保留 Godot 对象生命周期；组件以运行时 `Type` 精确索引，同时提供泛型读取和查询入口。实体 ID 单调递增，查询返回新数组；系统调度使用注册令牌区分同一实例在更新期间的移除与重新注册。
- 使用 `Godot.NET.Sdk/4.7.2` 与 `net8.0`。核心测试改为 C# `SceneTree` 脚本，由 Godot .NET 版运行；检查脚本先构建 C# 项目再运行场景与测试。
- C# 类型和文件采用 PascalCase；文档更新为 C# 示例。旧 `.gd` 和 UID 文件删除，避免双实现。

## Risks / Trade-offs

- [玩法调用方暂时不兼容] → 经 `main` PR 合入后，在 `develop` 单独迁移所有直接继承 ECS 基类的脚本及其调用方。
- [缺少 .NET 版 Godot 编辑器] → 检查脚本明确要求 4.7.2 .NET 版，并提供可配置的 `GODOT_BIN`。
- [Godot 对象需要显式释放] → 测试释放创建的核心对象；文档说明生命周期责任。

## Migration Plan

在 `main` 完成核心代码、测试与文档并运行检查；提交本地变更。之后由用户明确要求推送时通过目标为 `main` 的 PR 合并，再通过目标为 `develop` 的 PR 同步，最后迁移玩法调用方。

## Open Questions

具体仓库的存储操作与生命周期留待后续 change 决定。
