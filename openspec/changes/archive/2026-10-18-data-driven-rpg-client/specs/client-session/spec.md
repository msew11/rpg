## ADDED Requirements

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
