# Spec Delta

## MODIFIED Requirements

### Requirement: 环境参数统一管理
客户端 SHALL 由位于 src/Config/EnvService.cs、命名空间为 Rpg.Config 的无依赖 EnvService 统一持有配置、Schema、设置与模拟服务端目录，通过 GameServices.Env 只读访问。正式启动 SHALL 使用无参 GameServices.Initialize()；配置服务公开构造 SHALL 只接收 EnvService，设置服务 SHALL 接收 EnvService 与 LogService；网络服务 SHALL 显式接收 EnvService、EventBusService、ConfigService 与 LogService，并将已加载规则复制为服务端独立只读配置，账号服务 SHALL 显式接收 NetService；其他正式服务构造 SHALL 只接收前置服务依赖。顶层注册服务的构造 SHALL 集中在无参 Initialize 方法体内，按 Env、Log、EventBus、Config、Setting、Net、Session、World、Animation、Items 的顺序显式书写；Register 和注册句柄构造 SHALL 只接收现成实例，不再创建服务，不引入容器、反射或自动排序。显式 V2 接入 SHALL 允许 NetService 接管已构造的 MockServerService；该子服务只通过构造借用同次启动的 ConfigService，并由 Net 负责字节收发及释放，不增加全局入口。内部测试 SHALL 能覆盖环境目录和网络传输，不增加正式启动参数。

#### Scenario: 无参启动与默认目录
- **WHEN** 主场景调用 GameServices.Initialize()
- **THEN** 环境先于其消费者创建，配置和 Schema 从 res://config 与 res://schemas 加载，设置与模拟存档分别使用 user://settings 和 user://server，节点不再传递目录与工厂

#### Scenario: 隔离环境与生命周期
- **WHEN** 测试提供独立目录并注册服务，随后释放并重新启动
- **THEN** 配置、设置和模拟存档使用该环境的目录，环境随整组服务最后释放，未初始化或释放后访问环境入口报错，旧句柄不影响新环境

#### Scenario: 构造失败恢复
- **WHEN** 配置、设置或网络构造失败
- **THEN** 已成功构造的服务被清理，不留下注册入口或事件域，后续启动仍可成功

### Requirement: 服务依赖必须遵循固定线性顺序
服务 SHALL 只通过构造取得依赖，不得访问 GameServices、循环依赖、属性回填或延迟解析。顶层固定构造顺序 SHALL 为 Env、Log、EventBus、Config、Setting、Net、Session、World、Animation、Items，每个服务仅依赖前置服务。显式 V2 模式的 MockServerService SHALL 在 Config 之后、Net 之前构造，作为 Net 拥有的子服务先于 Config 释放；子服务不依赖 Net，不另行注册全局入口。整体释放 SHALL 先撤销全局入口，再严格逆序释放，单个服务失败仍继续其余清理；不增加释放前生命周期。EventBus SHALL 与其他服务统一保存在完整注册中，网络提前释放不得阻止访问仍有效的事件服务。

#### Scenario: 清理不依赖全局入口
- **WHEN** 完整服务组撤销全局入口后释放已登录账号
- **THEN** Session 通过构造绑定的 Net 隔离旧消息，Net 通过构造绑定的 EventBus 清理队列，所有服务按逆序释放且不访问全局入口

#### Scenario: 构造边界检查
- **WHEN** 检查全部正式服务及其内部类型的依赖调用
- **THEN** 不存在 GameServices 访问，服务构造参数中的服务均位于固定顺序前方，无循环或后置回填

#### Scenario: 网络单独释放
- **WHEN** 网络被提前释放而整体注册仍存在
- **THEN** 所有入口仍返回各自注册实例，IsInitialized 仍为 true；网络业务操作抛出已释放错误，事件仍能分发，整体所有者仍可完成清理
