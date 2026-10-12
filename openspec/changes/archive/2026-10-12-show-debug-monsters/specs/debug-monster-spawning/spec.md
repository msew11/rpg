# Spec Delta

## ADDED Requirements

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
