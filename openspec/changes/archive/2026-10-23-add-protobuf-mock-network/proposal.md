# Proposal

## Why

现有 MockStateSource 直接共享客户端对象，不能验证消息协议、服务器独立存储或缓存先于 UI 的分发契约。需要把现有角色纵切等价迁移到真实 protobuf 字节链路，为将来替换传输提供边界。

## What Changes

- 增加 NetManager、进程内传输、protobuf 消息注册与构建生成。
- 增加独立 MockServer 与角色 JSON 原子持久化。
- 增加缓存优先的 Session EventSystem；DataCache 组合 RoleCache。
- **BREAKING** 移除 IStateSource/MockStateSource，登录、选角、移动及 UI 改用 Send 和类型监听。
- 保留演示账号、角色性别、延迟校正与本地设置，不增加背包功能。

## Capabilities

### New Capabilities
- `client-network`: protobuf 字节收发、协议路由、队列与会话代次隔离。
- `session-events`: 可释放的类型监听、缓存优先、重入队列与异常隔离。
- `mock-server`: 独立服务端角色模型、请求处理、主动推送与 JSON 落地。

### Modified Capabilities
- `client-session`: 登录选角改走网络协议，角色缓存由 RoleCache 维护并隔离生命周期。

## Impact

影响 Session、UI 主控制器和 HUD、现有客户端及角色测试。增加 Google.Protobuf 3.36.2 与仅构建使用的 Grpc.Tools 2.84.0；proto 位于根目录，生成文件位于 src/Net/Message。更新长期文档与路线图。真实连接、RPC 等待、背包与玩法仍待后续确认。
