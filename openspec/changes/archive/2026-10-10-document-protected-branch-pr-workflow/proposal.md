# Proposal

## Why

仓库文档说明了 `main` 与 `develop` 的文件归属，但尚未说明这两个远程分支禁止直接推送以及更新时需要走 PR，容易按旧流程尝试直接推送。

## What Changes

- 在 `README.md` 增加贡献者可见的分支更新步骤。
- 在 `AGENTS.md` 明确 agent 提交和同步两条长期分支时遵循相同的 PR 流程。

## Capabilities

此项只补充仓库协作约定，不改变游戏能力；跳过规格增量。

## Impact

`main` 维护的 `README.md` 与 `AGENTS.md`。不修改游戏代码或远程仓库设置。
