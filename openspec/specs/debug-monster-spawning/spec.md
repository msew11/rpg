# debug-monster-spawning Specification

## Purpose
定义调试者在运行中的连续世界手动创建 monster 的最小能力，并明确新实体的位置与区块归属，供后续功能独立扩展。

## Requirements

### Requirement: 从 Debug 面板创建区块 monster
系统 SHALL 允许调试者在游玩时通过 Debug 面板创建一个 monster 实体；该实体 SHALL 归属玩家当前所在的活跃区块，且位置在该行走区块范围内随机选取。

#### Scenario: 在当前区块创建
- **WHEN** 调试者在游玩时按下创建 monster 按钮
- **THEN** 新增一个可由 monster 标记识别的实体，父实体是玩家当前区块实体，位置位于该区块内

#### Scenario: 区块卸载后恢复
- **WHEN** 已创建 monster 的区块卸载后重新加载
- **THEN** monster 实体随区块恢复，并保留其位置与归属

### Requirement: 创建后显示 monster 占位标记
系统 SHALL 在游戏画面中按 monster 的世界位置显示可辨认的临时标记；由 Debug 面板创建的 monster SHALL 立即出现在玩家当前视野内。

#### Scenario: 点击创建后可见
- **WHEN** 调试者在游玩时点击创建 monster
- **THEN** 当前 chunk 增加一个可见的 monster 标记，位置与新实体一致，且位于镜头当前视野内

#### Scenario: 随区块卸载和恢复
- **WHEN** monster 所属区块离开加载范围后重新进入
- **THEN** 标记随卸载消失，并在重新加载后于实体位置再次出现

#### Scenario: 回环区块的显示位置
- **WHEN** 玩家通过世界回环进入同一规范区块的另一个行走实例
- **THEN** monster 标记显示在当前行走实例对应的位置
