## MODIFIED Requirements

### Requirement: 环境参数统一管理
客户端 SHALL 由无依赖的 EnvironmentService 统一持有配置、Schema、设置与模拟服务端目录，通过 GameServices.Environment 只读访问。正式启动 SHALL 使用无参 GameServices.Initialize()；配置、设置与网络服务的公开构造 SHALL 只接收 EnvironmentService，其他正式服务构造 SHALL 只接收服务依赖。全部正式服务的构造 SHALL 集中在无参 Initialize 方法体内，按 Environment、Events、Config、Setting、Net、Session、World、Animation 的顺序显式书写；Register 和注册句柄构造 SHALL 只接收现成实例，不再创建服务，不引入容器、反射或自动排序。内部测试 SHALL 能覆盖环境目录和网络传输，不增加正式启动参数。

#### Scenario: 无参启动与默认目录
- **WHEN** 主场景调用 GameServices.Initialize()
- **THEN** 环境先于其消费者创建，配置和 Schema 从 res://config 与 res://schemas 加载，设置与模拟存档分别使用 user://settings 和 user://server，节点不再传递目录与工厂

#### Scenario: 隔离环境与生命周期
- **WHEN** 测试提供独立目录并注册服务，随后释放并重新启动
- **THEN** 配置、设置和模拟存档使用该环境的目录，环境随整组服务最后释放，未初始化或释放后访问环境入口报错，旧句柄不影响新环境

#### Scenario: 构造失败恢复
- **WHEN** 配置、设置或网络构造失败
- **THEN** 已成功构造的服务被清理，不留下注册入口或事件域，后续启动仍可成功
