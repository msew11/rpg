# Proposal: 补齐 ErisEsra 动作图

## Why

玩家模板目录目前只有待机和行走图，而已下载的素材包还提供五种动作。项目需要保留可用于后续动作接入的完整独立 PNG，并说明哪些动作已在游戏中播放。

## What Changes

- 为 16×16 和 16×32 素体补入跑步、攻击、交互、跳跃、转身精灵表。
- 在素材来源说明中列出每种动作的帧布局与当前运行状态。

## Capabilities

### Modified Capabilities

- `player-visual-template`: 提供当前素材包已有的独立动作表，方便后续功能按动作引用。

## Impact

新增资源和文档；当前移动操作与 Debug 面板模板选择保持原有行为。
