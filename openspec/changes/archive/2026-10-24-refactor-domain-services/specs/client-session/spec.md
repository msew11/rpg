## MODIFIED Requirements

### Requirement: 模拟登录与选角
客户端 SHALL 通过请求协议将用户名和密码交给模拟服务端；成功后 LoginRt 返回至少两个不同 ID 的 RoleSummaryPb，仅包含角色 ID、名称、等级与性别，客户端此时由 SessionService 在内部建立账号会话缓存；失败时展示错误且不创建会话。SessionService SHALL 直接持有 LoginCache 与 RoleCache。选角 SHALL 复用该会话并在请求前清空角色数据、隔离旧网络代次，SelectRoleRt 成功时返回 RoleWorldStatePb 完整角色世界状态，并在更新缓存后创建世界与界面；失败时只清理角色数据，保留账号会话和登录摘要。

#### Scenario: 演示账号登录
- **WHEN** 输入 `demo/demo`
- **THEN** 在 SessionService 内建立账号状态，LoginCache 保存 RoleSummaryPbData 列表供选角界面读取，RoleCache 尚未初始化；登录协议不返回生命、魔力、经验、坐标、版本或移动确认序号等世界状态

#### Scenario: 选角加载世界状态
- **WHEN** 登录后选择角色
- **THEN** SelectRoleRt 返回该角色最新的完整 RoleWorldStatePb，客户端据此初始化权威缓存与世界

#### Scenario: 错误凭据
- **WHEN** 凭据不匹配演示账号
- **THEN** 显示失败反馈且不建立账号缓存

#### Scenario: 角色切换
- **WHEN** 在一名角色的会话后选择另一角色
- **THEN** 保留同一账号 SessionService 与 LoginCache，清空 RoleCache 后加载新角色快照，旧消息不更新新角色数据，角色设置相互独立

#### Scenario: 选角失败
- **WHEN** 角色不存在或角色存档无法读取
- **THEN** 展示错误并清理待选角色数据，保留账号会话与登录摘要，不创建世界或 HUD

### Requirement: 登录摘要缓存
LoginCache SHALL 继承 CacheBase，由 SessionService 在成功登录响应中创建并持有；构造时将首份 LoginRt 中的 RoleSummaryPb 完整转换为 RoleSummaryPbData，只读保存角色列表与登录成功状态，后续登录结果在缓存阶段处理。UI SHALL 从会话缓存读取摘要，不自行转换或另存角色列表。LoginCache SHALL 跨角色切换保留，返回登录时随账号会话一起释放。初始非法成功响应 SHALL 报告错误且不创建账号会话，不遗留订阅或部分摘要；后续非法响应 SHALL 清空旧摘要并报告错误，不通知普通监听。

#### Scenario: 登录摘要先于 UI 更新
- **WHEN** 收到有效成功登录响应
- **THEN** 普通监听读取已转换的只读摘要，原协议及外部集合操作不能修改缓存

#### Scenario: 返回选角与返回登录
- **WHEN** 离开角色世界后返回选角，再返回登录
- **THEN** 返回选角时保留 SessionService 并复用 LoginCache 摘要，返回登录时由 SessionService 释放两个缓存并清空账号状态；重登成功创建新账号会话

#### Scenario: 登录响应失败或损坏
- **WHEN** 重新认证失败或成功响应包含无效角色摘要
- **THEN** 旧账号摘要被清空，不留下部分转换结果，合法重试仍可更新缓存

### Requirement: 会话缓存权威数据
SessionService SHALL 直接持有 RoleCache，由其维护角色数据，不设置 DataCache 中间容器或独立 BagCache。协议纯数据结构 SHALL 重新组装成独立的同名 PbData 数据类，不保留可变 protobuf 对象引用；所有 PbData 类 SHALL 位于 Session/Data，并在各自文件内提供 FromPb 与 ToPb 双向转换方法，转换时校验数据，输出新的协议对象。RoleWorldStatePb SHALL 转为 RoleWorldStatePbData 存入 RoleCache，登录摘要 RoleSummaryPb SHALL 转为 RoleSummaryPbData 存入 LoginCache。响应及推送 SHALL 先通过缓存监听写入较新 revision 的当前角色快照，再通知普通监听；UI 和世界读取客户端独立快照，世界更新仍经 Action。

RoleWorldStatePbData SHALL 直接通过构造参数与属性保存角色 ID、名称、等级、生命、魔力、经验、性别、逻辑位置及权威版本和确认序号，不嵌套额外的角色数据对象，位置仅保存一份。

#### Scenario: 状态更新
- **WHEN** 服务端返回较新的角色位置状态
- **THEN** 缓存先更新且世界协调器收到可转换为 Action 的消息通知

#### Scenario: 过期状态与其他角色
- **WHEN** 收到旧 revision 或其他角色状态
- **THEN** 当前角色缓存不会被回滚或替换

#### Scenario: 协议与缓存数据隔离
- **WHEN** 选角响应、移动响应或状态推送更新角色缓存后，其他监听修改原协议对象
- **THEN** 缓存中的 RoleWorldStatePbData 与已有旧快照保持独立，不随协议对象变化

#### Scenario: 数据转回协议
- **WHEN** 将有效 PbData 通过 ToPb 转回协议结构
- **THEN** 保留全部业务字段并返回独立对象，修改返回对象不影响原数据，世界状态的坐标使用当前权威 Position

### Requirement: 功能缓存统一订阅生命周期
所有功能缓存 SHALL 继承 CacheBase，通过受保护的 Subscribe 方法在缓存阶段注册协议，由基类私有集合托管订阅句柄。Dispose SHALL 幂等取消自身全部订阅，再执行一次可重写的 OnDispose 清理钩子；释放后不得重新订阅，且不影响其他缓存。SessionService SHALL 负责其持有的 LoginCache 与 RoleCache 的创建和释放；离开或切换角色时只重置角色数据，不释放账号缓存的订阅。

#### Scenario: 基类订阅缓存优先
- **WHEN** 功能缓存通过基类注册协议，普通监听早于缓存注册
- **THEN** 缓存仍先处理协议，普通监听读取更新后的数据

#### Scenario: 缓存重复释放
- **WHEN** 同一缓存被释放多次后继续分发消息
- **THEN** 该缓存不再接收任何已订阅类型，额外清理只执行一次，其他缓存继续接收消息，重新订阅抛出已释放错误
