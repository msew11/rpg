# client-session Specification

## Purpose
TBD - created by archiving change data-driven-rpg-client. Update Purpose after archive.
## Requirements
### Requirement: 模拟登录与选角
客户端 SHALL 将用户名和密码交给模拟状态源；成功后返回至少两个不同 ID 的角色，失败时展示错误。选角 SHALL 创建独立会话。

#### Scenario: 演示账号登录
- **WHEN** 输入 `demo/demo`
- **THEN** 显示可选择的角色列表

#### Scenario: 错误凭据
- **WHEN** 凭据不匹配演示账号
- **THEN** 显示失败反馈且不创建会话

#### Scenario: 角色切换
- **WHEN** 在一名角色的会话后选择另一角色
- **THEN** 旧会话释放，新会话具有独立缓存与角色设置

### Requirement: 会话缓存权威数据
`GameSession` SHALL 持有 `DataCache`，模拟源后续提供的角色与世界状态 SHALL 更新缓存并通知订阅者。

#### Scenario: 状态更新
- **WHEN** 模拟源返回较新的角色位置状态
- **THEN** 缓存更新且世界协调器收到可转换为 Action 的通知

### Requirement: 角色性别随会话传递
角色摘要与权威缓存 SHALL 保留明确的男性或女性属性，演示账号 SHALL 包含两种性别的可选角色；选角与后续状态通知 SHALL 不丢失该属性。

#### Scenario: 男女角色初始化
- **WHEN** 登录演示账号并分别选择两个角色
- **THEN** 各会话的角色与初始缓存保留对应性别

#### Scenario: 后续状态保留性别
- **WHEN** 模拟源发送选中角色的移动确认
- **THEN** 更新后的缓存仍具有该角色的性别
