## Context

`main` 的 ECS 测试位于 `tests/test_ecs.gd`；`tools/check.ps1` 用固定资源路径调用它。`develop` 有自己的功能测试，现有工作流仅比较 `scripts/core/`。

## Goals / Non-Goals

**Goals:** 明确核心测试与开发测试的目录归属，并在目标为 `develop` 的检查中保护核心测试。

**Non-Goals:** 更改 ECS 的断言或要求 `develop` 的全部测试与 `main` 一致。

## Decisions

- `tests/core/` 归 `main` 维护；开发分支的功能测试放在其余测试路径。同步迁移 `.gd.uid`，保留脚本身份。
- 将 `tools/check.ps1` 的 ECS 路径改为 `res://tests/core/test_ecs.gd`。
- GitHub 工作流沿用现有 `origin/main` 与 `HEAD` 的目录比较，对 `tests/core/` 检查新增、修改和删除。工作流和归属规则由 `main` 更新，`develop` 经合并取得。

## Risks / Trade-offs

- [旧资源路径失效] → 搜索引用并运行项目检查。
- [`develop` 落后于 `main`] → 合并最新 `main` 后重新检查。
