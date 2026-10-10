# Design

## Context

`main` 已提交文档归属规则，并移除三份长期文档。`develop` 在合并前包含这些文档的完整内容，但 README 混入原型玩法说明且缺少最新的导出说明。

## Goals / Non-Goals

**Goals:**
- `develop` 的 `AGENTS.md`、`README.md` 与 `main` 完全一致。
- 三份长期文档在 `develop` 中保持原样。

**Non-Goals:**
- 重写玩法、架构或路线内容。

## Decisions

- 用 Git 合并保留祖先关系；对共享文件取 `main` 版本，对三份长期文档取合并前 `develop` 版本。
- 通过文件内容比较和 `tools/check.ps1` 验证结果，而不是依赖冲突标记消失。

## Risks / Trade-offs

- 合并中三份文档呈现删除或修改冲突；逐个恢复 `develop` 版本后再提交。
