# Design

## Context

三组接口均有 LoadAsync/SaveAsync，账号摘要额外提供 NextRoleIdAsync，角色与地图包含提交恢复。模块 Cache 组织 DataCache，负责内存值、修改及成功保存基线；Actor 在既有生命周期调用 IO。现有仓库前缀约定适用于仓库类，本次将这三组类型明确为存储实现。

## Goals / Non-Goals

统一模块持久化类型与调用点命名，保持方法、可空参数、IO 顺序、失败恢复和 UID 对应关系。Cache 不改名，不迁移 IO 职责，不增加兼容包装，不修改其他 Repo 类型。

## Decisions

- 用户选择 Storage：相比只强调加载的 Loader，覆盖当前写入和恢复契约。ILoginStorage 与 LoginCache 对齐，替代按单份数据命名的 IAccountRoleSummaryRepo。
- 接口和实现使用 I + 模块名 + Storage、模块名 + Storage，沿用各模块 Storage 目录。同步测试替身名称、Actor 的 storage 参数和字段、注释及当前文档；归档历史保留原记录。
- 不改变规格行为，因此跳过 delta specs。通过原有存储、Actor 和共同提交测试验收，无需新增仅检查类型名的测试。
- 重命名 .cs 时一并移动原 .cs.uid。单独的 RepoItemBatch、RepoRoleIdSequence 不扩展更名范围，只更新其依赖类型与相关说明。

## Risks / Trade-offs

- 漏改引用或 UID → 全仓扫描六个旧名称、检查 UID 内容保留并编译全部测试。
- 行为意外变化 → 审查机械替换差异，运行项目检查和既有 .NET/Godot 验收。
- 用户已有改动与本次同文件 → 保存初始差异，只提交本次接口引用修改，保持原改动未提交。

## Migration Plan

一次迁移所有仓库内引用，更新架构、服务器文档与路线图。检查完成后记录结果并归档 change，保留本地约定式提交；整体回滚该提交可恢复旧命名，无存档迁移。
