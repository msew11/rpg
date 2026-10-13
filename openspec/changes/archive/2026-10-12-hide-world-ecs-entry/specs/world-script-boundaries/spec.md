# Spec Delta

## ADDED Requirements

### Requirement: 世界管理器封闭裸 ECS 入口

世界管理器 SHALL 自行持有和调度 ECS，生产调用方 SHALL 无法通过世界管理器取得可直接增删实体、组件或 System 的 ECS 实例。现有交互命令、世界读取和存档行为 SHALL 保持可用。

#### Scenario: 生产调用方检查世界入口

- **WHEN** 生产调用方使用世界管理器的公开接口
- **THEN** 无法从该接口取得可直接修改实体、组件或 System 的 ECS 实例

#### Scenario: 实体随区块保存和恢复

- **WHEN** 区块内实体在保存、卸载和重新加载后被检查
- **THEN** 实体的父子关系和持久组件仍按既有规则恢复
