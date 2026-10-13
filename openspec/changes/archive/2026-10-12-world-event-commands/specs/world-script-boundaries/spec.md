# Spec Delta

## ADDED Requirements

### Requirement: 世界交互由事件系统承接

世界管理器 SHALL 将交互事件转发给 ECS；具体移动意图、地块、调试开关和怪物生成规则 SHALL 位于世界 System 中，避免管理器按交互类型扩展方法。

#### Scenario: 检查新增交互位置
- **WHEN** 开发者查看交互命令实现
- **THEN** 世界管理器仅执行事件转发和变化通知，具体规则可在对应 System 中找到
