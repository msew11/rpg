## ADDED Requirements

### Requirement: 唯一全局事件服务
客户端 SHALL 只由 GameServices 创建一个实现 IService 的 EventService，并以只读 Events 入口提供给网络、会话、缓存、世界和 UI；消费者 SHALL 不创建私有事件系统，不经网络或缓存构造参数逐层传递。事件服务 SHALL 在消费者之前就绪，整体退出时最后释放；初始化前及释放后访问明确失败。监听异常 SHALL 通过 Events.Error 报告。

#### Scenario: 网络与缓存共享事件域
- **WHEN** 网络分发响应，缓存和 UI 通过 GameServices.Events 订阅
- **THEN** 所有监听使用同一个事件服务，缓存优先规则不变，额外网络实例不创建私有事件系统

#### Scenario: 网络释放和整体重建
- **WHEN** 一个网络实例释放，随后整体服务退出并重新启动
- **THEN** 单独释放网络不销毁事件服务；整体退出清除旧监听和待处理消息，新启动创建新的唯一事件服务，旧句柄不能释放新服务
