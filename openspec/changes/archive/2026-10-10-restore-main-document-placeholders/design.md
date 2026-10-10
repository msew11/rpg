## Context

`main` 当前缺少三份文档；`develop` 有独立维护的正文。此前的约定把“只由 `develop` 维护正文”误写成 `main` 不包含文件。

## Goals / Non-Goals

- 目标：`main` 跟踪三份空白文件，并使分支约定准确描述文件归属。
- 目标：合并 `main` 到 `develop` 时继续保留 `develop` 的正文。
- 非目标：恢复 `main` 曾有的旧正文，或修改 `develop` 的内容。

## Decisions

- 使用真正零字节的 Markdown 文件作为占位，避免标题或说明文字被误认为 `main` 的正文。
- 在 `AGENTS.md` 规定合并时保留 `develop` 版本，`README.md` 同步对读者说明。

## Risks / Trade-offs

首次将占位文件合并到 `develop` 时，若 Git 提示冲突，应选择保留 `develop` 的正文；不能用空白占位覆盖它。
