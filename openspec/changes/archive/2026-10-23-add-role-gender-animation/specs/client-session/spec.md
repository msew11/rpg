# Spec Delta

## ADDED Requirements

### Requirement: 角色性别随会话传递
角色摘要与权威缓存 SHALL 保留明确的男性或女性属性，演示账号 SHALL 包含两种性别的可选角色；选角与后续状态通知 SHALL 不丢失该属性。

#### Scenario: 男女角色初始化
- **WHEN** 登录演示账号并分别选择两个角色
- **THEN** 各会话的角色与初始缓存保留对应性别

#### Scenario: 后续状态保留性别
- **WHEN** 模拟源发送选中角色的移动确认
- **THEN** 更新后的缓存仍具有该角色的性别
