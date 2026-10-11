## Why

`main` 的 ECS 契约测试目前直接放在 `tests/` 根目录，与 `develop` 的功能测试缺少明确目录边界。现有分支检查只保护 `scripts/core/`，无法防止开发分支改动核心测试。

## What Changes

- 将 `main` 维护的 ECS 测试及 UID 移到 `tests/core/`。
- 更新检查脚本的测试资源路径。
- 更新 ECS 文档中的测试路径。
- 在 `AGENTS.md` 说明 `tests/core/` 的分支归属，并在 GitHub 工作流中要求 `develop` 的该目录与最新 `main` 一致。

## Capabilities

### New Capabilities

- `core-test-ownership`：核心测试的目录归属与分支一致性检查。

### Modified Capabilities

无；现有 ECS 验收场景保持一致。

## Impact

涉及 `tests/`、`tools/check.ps1`、`docs/ecs.md`、`AGENTS.md` 和 `.github/workflows/branch-document-ownership.yml`，不改变产品运行时行为。
