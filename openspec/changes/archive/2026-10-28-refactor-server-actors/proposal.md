# Proposal

## Why

服务端目前共享一个可写缓存并由 MockServer 集中分派请求，无法建立分片独立的执行和生命周期边界。改用串行邮箱 Actor，明确登录摘要、角色实体和地图状态的归属。

## What Changes

- BaseActor 使用独立异步任务与串行邮箱，提供 INIT/RUN/STOP、Ask/Tell 和错误传播。
- 启动单例 LoginActor；带角色或地图 ID 的消息按需建立 RoleActor、WorldActor，Role 初始化后 Ask 对应 MapId。
- ServerLoginCache、ServerRoleCache、ServerWorldCache 分别持有各领域状态；Handler 保持纯内存处理。
- 保留协议、延迟移动、AOI、共同落地与失败重试；删除旧集中缓存及准备路由。

## Capabilities

### New Capabilities
- `server-actors`: Actor 生命周期、串行处理、分片初始化与 Ask/Tell。

### Modified Capabilities
- `mock-server`: 按 Actor 所属领域加载与处理。

## Impact

影响 src/Server、MockTransport 生命周期、服务端测试与架构和路线图。客户端 protobuf 与存档格式不变，不增加玩法决定。
