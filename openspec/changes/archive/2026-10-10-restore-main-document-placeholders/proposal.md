## Why

提交 `f751f7ab` 删除了 `main` 中的三份长期文档。用户原意是保留空白文件作为占位，同时让 `develop` 独立维护正文。当前仓库状态与该意图不符，也让文档中的文件归属说明产生误导。

## What Changes

- 在 `main` 恢复三份零字节 Markdown 占位文件。
- 更新 `AGENTS.md`、`README.md`，明确 `main` 留空、`develop` 维护正文及合并时保留正文。

## Capabilities

仅修正仓库文档和分支约定，不改变游戏能力，跳过规格增量。

## Impact

影响 `docs/architecture.md`、`docs/game-vision.md`、`docs/roadmap.md`、`AGENTS.md` 和 `README.md`。`develop` 中现有正文不修改。
