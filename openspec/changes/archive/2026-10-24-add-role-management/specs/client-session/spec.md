## MODIFIED Requirements

### Requirement: 模拟登录与选角
客户端 SHALL 通过请求协议将用户名和密码交给模拟服务端；成功后 LoginRt 返回当前未删除角色的 RoleSummaryPb 列表（允许为空），仅包含角色 ID、名称、等级、性别与头型；首次初始化提供两个不同性别的演示角色，登录缓存此时在缓存阶段完成认证；SessionService 可在请求前建立未认证的空缓存，失败时展示错误且不建立有效账号会话。SessionService SHALL 直接持有 LoginCache 与 RoleCache。选角 SHALL 复用该会话并在请求前清空角色数据、隔离旧网络代次，SelectRoleRt 成功时返回 RoleWorldStatePb 完整角色世界状态，并在更新缓存后创建世界与界面；失败时只清理角色数据，保留账号会话和登录摘要。

#### Scenario: 演示账号登录
- **WHEN** 输入 `demo/demo`
- **THEN** 在 SessionService 内建立账号状态，LoginCache 保存 RoleSummaryPbData 列表供选角界面读取，RoleCache 尚未初始化；登录协议不返回生命、魔力、经验、坐标、版本或移动确认序号等世界状态

#### Scenario: 选角加载世界状态
- **WHEN** 登录后选择角色
- **THEN** SelectRoleRt 返回该角色最新的完整 RoleWorldStatePb，客户端据此初始化权威缓存与世界

#### Scenario: 错误凭据
- **WHEN** 凭据不匹配演示账号
- **THEN** 显示失败反馈且不建立有效账号会话；当前尝试的空缓存保持未认证且无角色摘要

#### Scenario: 角色切换
- **WHEN** 在一名角色的会话后选择另一角色
- **THEN** 保留同一账号 SessionService 与 LoginCache，清空 RoleCache 后加载新角色快照，旧消息不更新新角色数据，角色设置相互独立

#### Scenario: 选角失败
- **WHEN** 角色不存在或角色存档无法读取
- **THEN** 展示错误并清理待选角色数据，保留账号会话与登录摘要，不创建世界或 HUD

### Requirement: 角色性别随会话传递
角色摘要与权威缓存 SHALL 保留明确的男性或女性属性及头型编号，头型 0 按旧版默认头型 1 展示，首次初始化的演示账号 SHALL 包含两种性别的可选角色；选角与后续状态通知 SHALL 不丢失该属性。

#### Scenario: 男女角色初始化
- **WHEN** 登录演示账号并分别选择两个角色
- **THEN** 各会话的角色与初始缓存保留对应性别

#### Scenario: 后续状态保留性别
- **WHEN** 模拟源发送选中角色的移动确认
- **THEN** 更新后的缓存仍具有该角色的性别
