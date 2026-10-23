## MODIFIED Requirements

### Requirement: 模拟登录与选角
客户端 SHALL 通过请求协议将用户名和密码交给模拟服务端；成功后返回至少两个不同 ID 的角色，失败时展示错误。选角 SHALL 在请求前建立独立会话缓存监听，并在成功响应更新缓存后创建世界与界面；失败时释放待建立会话。

#### Scenario: 演示账号登录
- **WHEN** 输入 `demo/demo`
- **THEN** 显示可选择的角色列表

#### Scenario: 错误凭据
- **WHEN** 凭据不匹配演示账号
- **THEN** 显示失败反馈且不创建会话

#### Scenario: 角色切换
- **WHEN** 在一名角色的会话后选择另一角色
- **THEN** 旧会话释放，新会话具有独立缓存与角色设置

#### Scenario: 选角失败
- **WHEN** 角色不存在或角色存档无法读取
- **THEN** 展示错误并释放待建立会话，不创建世界或 HUD

### Requirement: 会话缓存权威数据
GameSession SHALL 持有组合功能缓存的 DataCache，本期由 RoleCache 维护角色状态。响应及推送 SHALL 先通过缓存监听写入较新 revision 的当前角色快照，再通知普通监听；UI 和世界读取客户端独立快照，世界更新仍经 Action。

#### Scenario: 状态更新
- **WHEN** 服务端返回较新的角色位置状态
- **THEN** 缓存先更新且世界协调器收到可转换为 Action 的消息通知

#### Scenario: 过期状态与其他角色
- **WHEN** 收到旧 revision 或其他角色状态
- **THEN** 当前角色缓存不会被回滚或替换
