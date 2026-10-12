# Spec Delta

## Purpose

定义调试者在运行中的连续世界手动创建 monster 的最小能力，并明确新实体的位置与区块归属，供后续功能独立扩展。

## ADDED Requirements

### Requirement: 从 Debug 面板创建区块 monster
系统 SHALL 允许调试者在游玩时通过 Debug 面板创建一个 monster 实体；该实体 SHALL 归属玩家当前所在的活跃区块，且位置在该行走区块范围内随机选取。

#### Scenario: 在当前区块创建
- **WHEN** 调试者在游玩时按下创建 monster 按钮
- **THEN** 新增一个可由 monster 标记识别的实体，父实体是玩家当前区块实体，位置位于该区块内

#### Scenario: 区块卸载后恢复
- **WHEN** 已创建 monster 的区块卸载后重新加载
- **THEN** monster 实体随区块恢复，并保留其位置与归属
