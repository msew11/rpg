# main-branch-scope Specification

## Purpose
规定 `main` 的空白项目框架范围，避免已移除的核心能力继续被视为该分支的现行能力。
## Requirements
### Requirement: main 保持空白项目框架

`main` SHALL 保留可运行的空白 Godot 项目入口、通用检查与导出工具及协作规范，且 SHALL 不维护 ECS、数据仓库等运行时实现或依赖这些实现的测试。

#### Scenario: 检查 main 的当前内容

- **WHEN** 开发者检查 `main` 的源文件、测试和现行 OpenSpec 规格
- **THEN** 不存在 ECS、数据仓库实现及其测试或现行能力规格，空白主场景仍可运行

#### Scenario: 开发分支增加运行时能力

- **WHEN** `develop` 实现新的运行时功能及测试
- **THEN** 这些功能与测试不要求先在 `main` 维护同一份实现

### Requirement: 个人 IDE 设置不进入共享仓库

`main` SHALL 忽略 Rider 的 `*.DotSettings.user` 个人设置文件。

#### Scenario: Rider 生成解决方案个人设置

- **WHEN** Rider 在仓库中生成 `rpg.sln.DotSettings.user`
- **THEN** Git 忽略该文件，且无需将其提交到仓库
