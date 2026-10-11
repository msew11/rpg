## Context

`scripts/core/ecs/` 已按模块收纳三个 ECS 脚本；数据仓库的两个抽象脚本仍直接位于 `scripts/core/`。ECS 测试由 `tools/check.ps1` 以文件路径运行，文档按测试用途描述。

## Goals / Non-Goals

**Goals:** 整理核心数据目录，简化 ECS 测试文件名，保持类引用和测试行为一致。

**Non-Goals:** 修改仓库或 ECS 接口、加入具体数据实现或改变验收场景。

## Decisions

- 使用 `scripts/core/data/` 收纳 `data_repo.gd` 与 `data_repo_cache.gd`，名称与现有 `ecs/` 平级，保留对应 UID 内容。
- 将测试改为 `test_ecs.gd` 和 `TestEcs`，并同步检查脚本路径；测试内容不变。
- 修改文档路径描述，避免残留旧文件名。

## Risks / Trade-offs

- [硬编码路径失效] → 全仓搜索旧路径，并执行 Godot 导入和统一检查。

## Migration Plan

在 `main` 移动文件并验证，归档 change 后本地提交；保留工作树中原有的 `.run/Editor.run.xml` 改动。
