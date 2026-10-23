## Why

主控制器和 HUD 持有配置仓库、设置、会话、世界与输入对象，网络入口虽已统一，业务状态和响应处理仍分散在 Godot 节点。按用户要求按功能建立服务，以收拢状态、缩短调用链并保留现有行为。

## What Changes

- 声明统一 IService 生命周期契约，GameServices 持有 Config、Setting、Net、Session、World、Animation 服务。
- 配置和设置对外通过服务访问，仓库成为内部加载与持久化细节；设置服务只提供当前值和业务修改方法。
- 账号会话、世界创建、输入转换和权威同步进入服务；节点通过 GameServices 调用，只拥有表现状态与节点资源。
- 保留网络实例隔离、缓存优先、Action 写入世界、启动失败清理与服务重建契约。

## Capabilities

### Modified Capabilities
- `game-services`: 从网络入口扩展为按功能持有全部客户端业务服务，并约束节点和服务的依赖边界。

## Impact

涉及 Services、UI、World/Views、Settings 与集成测试。同步更新架构、路线图；不改变协议、存档格式、玩法或显示行为。
