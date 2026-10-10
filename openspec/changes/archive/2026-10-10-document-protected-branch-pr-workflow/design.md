# Design

## Context

现有 README 只区分 `main` 与 `develop` 的用途；AGENTS 规定文档归属及通过合并同步 `main`，未说明远程受保护分支的 PR 流程。

## Goals / Non-Goals

**Goals:** 让贡献者和 agent 知道更新远程 `main`、`develop` 的具体步骤，并能判断 PR 的目标分支。

**Non-Goals:** 改动分支保护设置、游戏能力或分支文件归属。

## Decisions

- 在 README 写明从目标分支创建工作分支、提交、推送该工作分支、发起指向目标分支的 PR、通过 PR 合并。
- 在 AGENTS 补充禁止直接推送远程 `main` 与 `develop`；从 `main` 向 `develop` 同步也走面向 `develop` 的 PR。
- 文档改动以远程 `main` 为基线建立工作分支，避免本地 `main` 尚未合入的 ECS 提交进入本次 PR。

## Risks / Trade-offs

文档仅说明协作流程；远程分支保护规则仍由仓库设置执行。
