# 独立的全局事件服务

## Why
事件系统由网络持有，并在缓存构造与会话属性中逐层传递，造成归属不清。客户端应在一次启动中只使用一个事件服务。

## What Changes
- 将 EventSystem 的实现直接迁入 EventService，实现 IService，由 GameServices 唯一创建、最后释放。
- 消费者通过 GameServices.Events 访问，删除 Net.Events、Session.Events 和缓存事件构造参数。
- 网络只发布消息和取消旧会话分发，不拥有事件服务；监听错误由 Events.Error 报告。
- 测试共享事件服务，验证重置、释放、失败清理及重启隔离。

## Capabilities
### Modified Capabilities
- `session-events`: 唯一全局事件服务及独立生命周期。
- `game-services`: 增加 Events 入口，显式网络共享事件域。

## Impact
涉及服务注册、网络、缓存、世界、UI 及对应测试；不更改协议和事件分发顺序。
