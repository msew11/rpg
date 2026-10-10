# Proposal

## Why

当前两条长期分支的文档归属混杂：`main` 保存开发分支的方向文档，`develop` 的 README 又混入具体原型说明，后续合并容易反复污染文件。

## What Changes

- 在 `main` 的 `AGENTS.md` 明确五份文件的分支归属与合并时的处理规则。
- 从 `main` 移除仅由开发分支维护的三份长期文档，并修正 README 的引用。

## Capabilities

此项只调整仓库文档与分支工作约定，不改变游戏能力；跳过规格增量。

## Impact

`main` 的 `AGENTS.md`、`README.md`、`docs/architecture.md`、`docs/game-vision.md`、`docs/roadmap.md`。开发分支的对齐由后续独立 change 处理。
