# 存储命名重构验收

## 交付

| 原接口 / 实现 | 新接口 / 实现 |
| --- | --- |
| IAccountRoleSummaryRepo / RepoAccountRoleSummary | ILoginStorage / LoginStorage |
| IRoleRepo / RepoRole | IRoleStorage / RoleStorage |
| IWorldRepo / RepoWorld | IWorldStorage / WorldStorage |

类型文件和原 Godot UID 同步迁移，装配、Actor、共同提交依赖、测试替身与调用点均已更新。Cache 继续管理内存数据、修改与保存基线；存储保留原加载、保存、恢复及取号行为。架构、服务器文档和路线图同步更新；无规格行为变更，使用 skip_specs，不修改主规格或历史归档。

## 检查结果

- `dotnet build tests/ClientTests/ClientTests.csproj --nologo` 通过，零警告、零错误。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 一次完整通过：项目编译、导入、主场景，普通 .NET 138 项、Godot xUnit 267 项，共 405 项单元测试全部通过；16 个场景检查全部通过，包含多人 TCP、存档重启、角色管理、背包装备、地面丢弃/拾取、界面及角色设置。
- 原有存储及 Actor 测试继续覆盖缺失/损坏读取、初始化屏障、修改合并、保存失败重试、角色多表恢复、地图清单提交、共同批次回滚与重启恢复。纯命名重构无需新增只验证类型名称的测试。
- 对 42 个变更的 C# 文件逐个与 HEAD 对比，除预期类型、变量、文件引用名称及中文注释外代码一致；独立计入并排除用户原有字段限定名改动。6 个迁移 UID 与原值一致。
- 当前代码、测试、文档和主规格扫描六个旧名称无残留；归档保留历史名称供追溯。`git diff --check` 与 `openspec validate rename-server-storage-contracts --strict` 通过。

## 范围与工作区

本次不改变存档格式、协议、可空契约、业务规则或 IO 顺序；RepoItemBatch 和 RepoRoleIdSequence 保留独立名称。三个公开接口及实现的源码名称已变更，调用者应使用新名称，无旧名称兼容别名。

用户原有 ServerModules.cs 中将字段类型 `Modules.Common.Storage.RepoItemBatch` 简写为 `Common.Storage.RepoItemBatch` 的修改保留在工作区，不纳入本次本地提交。没有推送或创建 PR。
