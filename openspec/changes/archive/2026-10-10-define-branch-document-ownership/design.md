# Design

## Context

`main` 当前包含三份开发方向文档；`develop` 的 README 有玩法与测试区块细节，且没有继承 `main` 最新的导出说明。两分支的 AGENTS.md 原本相同。

## Goals / Non-Goals

**Goals:**
- 让分支归属规则在仓库内直接可见。
- 清理当前内容，并让后续合并能按规则保留各自负责的文件。

**Non-Goals:**
- 改动游戏代码或 OpenSpec 游戏能力规范。

## Decisions

- 从 `main` 删除三份长期文档，而非保留空壳；README 明确它们只在 `develop` 中。
- 分支规则放在由 `main` 维护的 `AGENTS.md`。其余文档仍按原有 OpenSpec 流程维护。
- `AGENTS.md` 记录后续合并准则；开发分支的实际对齐在独立 change 中完成。

## Risks / Trade-offs

- 普通 Git 合并可能再次引入被删除的文件或跨分支内容；合并后显式核对五份文件的归属。
