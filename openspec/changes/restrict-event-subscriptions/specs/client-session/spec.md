## MODIFIED Requirements

### Requirement: 模拟登录与选角
客户端 SHALL 通过请求协议将用户名和密码交给模拟服务端；成功后 LoginRt 返回至少两个不同 ID 的 RoleSummaryPb，仅包含角色 ID、名称、等级与性别，登录缓存此时在缓存阶段完成认证；SessionService 可在请求前建立未认证的空缓存，失败时展示错误且不建立有效账号会话。SessionService SHALL 直接持有 LoginCache 与 RoleCache。选角 SHALL 复用该会话并在请求前清空角色数据、隔离旧网络代次，SelectRoleRt 成功时返回 RoleWorldStatePb 完整角色世界状态，并在更新缓存后创建世界与界面；失败时只清理角色数据，保留账号会话和登录摘要。

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

### Requirement: 登录摘要缓存
LoginCache SHALL 继承 CacheBase，由 SessionService 创建并持有，允许在登录请求前创建空缓存；首份和后续 LoginRt 均由缓存实现类在缓存阶段完整转换 RoleSummaryPb 为 RoleSummaryPbData，只读保存角色列表与登录成功状态。未收到有效成功响应前 SHALL 保持未认证与空摘要；SessionService SHALL 不订阅登录响应。UI SHALL 从会话缓存读取摘要，不自行转换或另存角色列表。LoginCache SHALL 跨角色切换保留，返回登录时随账号会话一起释放。初始非法成功响应 SHALL 报告错误且不建立有效账号会话，不遗留部分摘要；空缓存仅保留当前尝试所需的托管订阅，重试、退出或服务释放 SHALL 回收旧订阅；后续非法响应 SHALL 清空旧摘要并报告错误，不通知普通监听。

#### Scenario: 登录摘要先于 UI 更新
- **WHEN** 收到有效成功登录响应
- **THEN** 普通监听读取已转换的只读摘要，原协议及外部集合操作不能修改缓存

#### Scenario: 返回选角与返回登录
- **WHEN** 离开角色世界后返回选角，再返回登录
- **THEN** 返回选角时保留 SessionService 并复用 LoginCache 摘要，返回登录时由 SessionService 释放两个缓存并清空账号状态；重登成功创建新账号会话

#### Scenario: 登录响应失败或损坏
- **WHEN** 重新认证失败或成功响应包含无效角色摘要
- **THEN** 旧账号摘要被清空，不留下部分转换结果，合法重试仍可更新缓存


#### Scenario: 首次响应由缓存处理
- **WHEN** 服务提交登录请求后收到首份合法成功响应
- **THEN** LoginCache 自身完成数据转换并进入已认证状态，普通 UI 监听读取完整摘要，服务没有协议订阅
