## Why

当前分支约定要求每次工作先创建工作分支并推送，导致完成一次工作就发生远程推送。用户希望在本地长期分支连续开发、积累提交，待明确要求推送时再集中发布。

## What Changes

- 允许直接在本地 `main`、`develop` 开发和提交。
- 将远程推送、远程分支和 PR 的创建推迟到用户明确要求；一次推送汇集指定分支全部未推送提交。
- 保留远程 `main`、`develop` 的 PR 合并要求，并使 `AGENTS.md` 与 `README.md` 一致。

## Capabilities

不改变游戏能力；此 change 仅调整仓库协作约定，因此跳过规格增量。

## Impact

影响 `AGENTS.md`、`README.md` 和后续 Git 操作方式，不改变运行代码。
