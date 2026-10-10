## Context

`main` 现在跟踪 `docs/architecture.md`、`docs/game-vision.md` 和 `docs/roadmap.md` 三个空文件，正文由 `develop` 维护。现有 workflow 的 `main` 检查仍以文件不存在为成功条件。

## Goals / Non-Goals

- 目标：在指向 `main` 的 PR 合并树和 `main` 的 push 上验证三份文件均存在且严格为零字节。
- 非目标：限制 `develop` 文档正文，或修改分支保护设置。

## Decisions

- 逐个读取 Git `HEAD` 树中的路径，要求模式为普通文件 `100644`、类型为 blob 且对象大小为 0。检查已提交内容，避免工作区状态影响结论。
- 保持现有触发条件、job 名称和 `develop` 检查不变，避免改变已有必需检查的标识。

## Risks / Trade-offs

`main` 的 PR 必须包含这三个占位文件；尚未合入占位文件的独立 PR 会失败，直到目标分支或该 PR 包含本次修正。
