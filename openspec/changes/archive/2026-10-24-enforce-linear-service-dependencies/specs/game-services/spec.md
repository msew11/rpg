## MODIFIED Requirements

### Requirement: 会话绑定与独立实例

账号会话 SHALL 仅通过构造显式绑定创建时的网络与事件服务，不得在服务内从全局入口获取依赖。全局服务释放或重新建立后，旧会话 SHALL 不能向新网络发送请求，其延后释放不影响新会话。网络服务和会话 SHALL 继续支持显式实例创建；显式网络保留各自请求队列，事件分发统一使用构造传入的同一 EventService，不得创建独立事件域。

#### Scenario: 全局会话与独立测试并存
- **WHEN** 显式传入已注册网络和事件服务创建账号会话，同时以显式独立网络创建另一个会话
- **THEN** 两者使用各自网络请求队列并共享唯一事件服务，独立网络的创建和释放不替换或释放全局事件服务

#### Scenario: 重启后旧会话延后释放
- **WHEN** 服务释放并重新注册，新账号会话已有待发送请求，此时旧会话尝试操作并释放
- **THEN** 旧会话操作明确失败，新服务的会话状态和待发送请求不受影响

### Requirement: 环境参数统一管理
客户端 SHALL 由无依赖的 EnvironmentService 统一持有配置、Schema、设置与模拟服务端目录，通过 GameServices.Environment 只读访问。正式启动 SHALL 使用无参 GameServices.Initialize()；配置与设置服务公开构造 SHALL 只接收 EnvironmentService；网络服务 SHALL 显式接收 EnvironmentService 与 EventService，账号服务 SHALL 显式接收 NetService 与 EventService；其他正式服务构造 SHALL 只接收前置服务依赖。全部正式服务的构造 SHALL 集中在无参 Initialize 方法体内，按 Environment、Events、Config、Setting、Net、Session、World、Animation 的顺序显式书写；Register 和注册句柄构造 SHALL 只接收现成实例，不再创建服务，不引入容器、反射或自动排序。内部测试 SHALL 能覆盖环境目录和网络传输，不增加正式启动参数。

#### Scenario: 无参启动与默认目录
- **WHEN** 主场景调用 GameServices.Initialize()
- **THEN** 环境先于其消费者创建，配置和 Schema 从 res://config 与 res://schemas 加载，设置与模拟存档分别使用 user://settings 和 user://server，节点不再传递目录与工厂

#### Scenario: 隔离环境与生命周期
- **WHEN** 测试提供独立目录并注册服务，随后释放并重新启动
- **THEN** 配置、设置和模拟存档使用该环境的目录，环境随整组服务最后释放，未初始化或释放后访问环境入口报错，旧句柄不影响新环境

#### Scenario: 构造失败恢复
- **WHEN** 配置、设置或网络构造失败
- **THEN** 已成功构造的服务被清理，不留下注册入口或事件域，后续启动仍可成功

## ADDED Requirements

### Requirement: 服务依赖必须遵循固定线性顺序
服务 SHALL 只通过构造取得依赖，不得访问 GameServices、循环依赖、属性回填或延迟解析。固定构造顺序 SHALL 为 Environment、Events、Config、Setting、Net、Session、World、Animation，每个服务仅依赖前置服务。整体释放 SHALL 先撤销全局入口，再严格逆序释放，单个服务失败仍继续其余清理；不增加释放前生命周期。Events SHALL 与其他服务统一保存在完整注册中，网络提前释放不得阻止访问仍有效的事件服务。

#### Scenario: 清理不依赖全局入口
- **WHEN** 完整服务组撤销全局入口后释放已登录账号
- **THEN** Session 通过构造绑定的 Net 隔离旧消息，Net 通过构造绑定的 Events 清理队列，所有服务按逆序释放且不访问全局入口

#### Scenario: 构造边界检查
- **WHEN** 检查全部正式服务及其内部类型的依赖调用
- **THEN** 不存在 GameServices 访问，服务构造参数中的服务均位于固定顺序前方，无循环或后置回填

#### Scenario: 网络单独释放
- **WHEN** 网络被提前释放而整体注册仍存在
- **THEN** 网络入口拒绝访问，事件入口仍返回同一未释放实例，整体所有者仍可完成清理
