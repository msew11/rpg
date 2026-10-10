# Proposal

## Why

`develop` 的 README 包含等轴移动和连续区块原型细节，偏离由 `main` 维护的共享说明；同时 `main` 已明确三份长期文档只由开发分支维护。

## What Changes

- 合并最新 `main`，并以 `main` 的 `AGENTS.md`、`README.md` 为准，移除 README 中的玩法和原型描述。
- 保留 `develop` 的 `docs/architecture.md`、`docs/game-vision.md`、`docs/roadmap.md` 内容。
- 验证分支文件归属、项目检查与 Windows 导出。

## Capabilities

仅做分支文档与合并维护，不改变游戏能力；跳过规格增量。

## Impact

`develop` 分支的共享文档、三份开发文档和 OpenSpec change；不修改游戏代码。
