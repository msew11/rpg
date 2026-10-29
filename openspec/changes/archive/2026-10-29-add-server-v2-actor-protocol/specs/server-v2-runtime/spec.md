## ADDED Requirements

### Requirement: 上下文借用分片定位能力
模块所有者 SHALL 管理目录、身份定位和生命周期，通过必需构造依赖向全部模块上下文提供只读 Actors 定位入口。该入口 SHALL 仅按账号、角色和地图身份返回消息引用，不暴露 Context、仓库或释放能力；定位 SHALL 不触发 INIT。上下文 SHALL 只借用此入口，不负责生命周期，不使用全局定位或可空回填。跨 Actor 业务步骤 SHALL 归所属模块 Async Handler，与本地 Handler 共用注册分发，不放入 Actor DispatchAsync；调用 SHALL 禁止自身 Ask 及循环等待。

#### Scenario: 服务定位隔离与生命周期
- **WHEN** 两个独立服务的 Handler 通过上下文定位相同身份
- **THEN** 各自取得本服务的引用且互不串用，未收消息的引用保持 INIT，结束上下文使用不停止服务；模块所有者关闭后引用停止且拒绝新定位

#### Scenario: 缺失定位依赖
- **WHEN** 构造模块上下文时缺少 Actors 定位依赖
- **THEN** 同步拒绝缺失参数，不使用测试空配置或稍后回填替代
