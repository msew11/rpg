# client-session Specification

## Purpose
TBD - created by archiving change data-driven-rpg-client. Update Purpose after archive.
## Requirements
### Requirement: 模拟登录与选角
客户端 SHALL 通过请求协议将用户名和密码交给模拟服务端；成功后 LoginRt 返回当前未删除角色的 RoleSummaryPb 列表（允许为空），包含角色 ID、名称、等级、性别、头型及 equipped_items 展示配置 ID；全新存档首次登录 SHALL 返回空角色列表，不自动生成演示角色，登录缓存此时在缓存阶段完成认证；SessionService 可在请求前建立未认证的空缓存，失败时展示错误且不建立有效账号会话。SessionService SHALL 直接持有 LoginCache 与 RoleCache。选角 SHALL 复用该会话并在请求前清空角色数据、隔离旧网络代次，EnterWorldRt 成功时并列返回可见 RoleWorldStatePb、私有 RolePb 与 BagPb，全部有效后才初始化缓存、世界和界面；失败时只清理角色数据，保留账号会话和登录摘要。

#### Scenario: 演示账号登录
- **WHEN** 输入 `demo1/123` 或 `demo2/123`（本机服务器模式使用 `admin/123`）
- **THEN** 在 SessionService 内建立账号状态，LoginCache 保存 RoleSummaryPbData 列表供选角界面读取，RoleCache 尚未初始化；登录协议不返回生命、魔力、经验、坐标、版本或移动确认序号等世界状态

#### Scenario: 首次登录为空列表
- **WHEN** 使用全新存档登录演示账号并重启后再次登录
- **THEN** 登录成功且角色列表均为空，仍可进入创建流程

#### Scenario: 选角加载世界状态
- **WHEN** 登录后选择角色
- **THEN** EnterWorldRt 返回该角色最新的可见世界状态、私有 Role 数据及背包，经验和背包扩容不出现在世界状态中

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

### Requirement: 会话缓存权威数据
SessionService SHALL 持有账号、角色及地图缓存，独立不可变 PbData 与 protobuf 对象隔离。进入响应 SHALL 完整校验身份、私有角色、物品、公开装备与地图后原子接纳。世界帧 SHALL 检查序号连续性并原子更新本人位置、地图和远端公开角色；本人装备仅随进入或 RoleItemsChangedNotice 与背包共同接纳。普通监听在缓存阶段成功后执行，世界更新经 Action。MoveRt 只确认 action_id，不包含位置。

#### Scenario: 状态更新
- **WHEN** 服务端返回较新的角色位置状态
- **THEN** 缓存先更新且世界协调器收到可转换为 Action 的消息通知

#### Scenario: 过期状态与其他角色
- **WHEN** 收到旧世界帧或其他角色的私有状态
- **THEN** 当前角色缓存不会被回滚或替换

#### Scenario: 协议与缓存数据隔离
- **WHEN** 选角响应、移动响应或状态推送更新角色缓存后，其他监听修改原协议对象
- **THEN** 缓存中的 RoleWorldStatePbData 与已有旧快照保持独立，不随协议对象变化

#### Scenario: 数据转回协议
- **WHEN** 将有效 PbData 通过 ToPb 转回协议结构
- **THEN** 世界 ToPb 仅保留可见字段、独立装备并使用当前权威 Position，不包含私有背包，私有数据由 ToRolePb 单独输出；返回对象均与原数据隔离

#### Scenario: 选角缺少私有领域
- **WHEN** 成功选角响应缺少 RolePb、背包或公开装备，或各部分角色 ID 与响应不符
- **THEN** 拒绝整份响应，不留下部分角色或背包缓存

#### Scenario: 本人物品通知先于普通监听
- **WHEN** 收到本人的较新完整物品通知
- **THEN** 先检查身份、两侧内容及跨列表唯一性，全部成功才发布两侧新快照，普通监听读取同次提交的背包与装备

#### Scenario: 本人物品版本回退与冲突
- **WHEN** 收到旧代次物品提交、当前提交中公开配置 ID 与私有穿戴实例不一致
- **THEN** 旧提交整份忽略；穿戴不一致报告错误；均不留下部分背包或装备替换

#### Scenario: 本人的公开装备消息交错
- **WHEN** 本人物品变更前后交错收到包含本人较新装备的世界帧公开增量或全量
- **THEN** 本人装备只随私有组合通知更新；世界帧仍更新有效位置、地图和远端角色，不产生物品短暂重复或消失

#### Scenario: 组合消息中一侧非法
- **WHEN** 较新本人物品通知中装备或背包非法，或实例重复或公开配置 ID 与私有穿戴实例不一致
- **THEN** 拒绝整条消息并保留两侧原快照，普通监听不处理该消息

#### Scenario: 切角清空独立装备
- **WHEN** 返回选角并进入另一角色，旧会话的装备或物品消息晚到
- **THEN** 旧装备与背包同时清空，新角色只从本次成功选角初始化，晚到消息不污染新角色

### Requirement: 功能缓存统一订阅生命周期
所有功能缓存 SHALL 继承 CacheBase，通过受保护的 Subscribe 方法在缓存阶段注册协议，由基类私有集合托管订阅句柄。Dispose SHALL 幂等取消自身全部订阅，再执行一次可重写的 OnDispose 清理钩子；释放后不得重新订阅，且不影响其他缓存。SessionService SHALL 负责其持有的 LoginCache 与 RoleCache 的创建和释放；离开或切换角色时只重置角色数据，不释放账号缓存的订阅。

#### Scenario: 基类订阅缓存优先
- **WHEN** 功能缓存通过基类注册协议，普通监听早于缓存注册
- **THEN** 缓存仍先处理协议，普通监听读取更新后的数据

#### Scenario: 缓存重复释放
- **WHEN** 同一缓存被释放多次后继续分发消息
- **THEN** 该缓存不再接收任何已订阅类型，额外清理只执行一次，其他缓存继续接收消息，重新订阅抛出已释放错误

### Requirement: 角色性别随会话传递
角色摘要与权威缓存 SHALL 保留明确的男性或女性属性及头型编号，头型 0 按旧版默认头型 1 展示，选角与后续状态通知 SHALL 不丢失该属性。

#### Scenario: 男女角色初始化
- **WHEN** 创建男女角色后分别选择两个角色
- **THEN** 各会话的角色与初始缓存保留对应性别

#### Scenario: 后续状态保留性别
- **WHEN** 模拟源发送选中角色的移动确认
- **THEN** 更新后的缓存仍具有该角色的性别
