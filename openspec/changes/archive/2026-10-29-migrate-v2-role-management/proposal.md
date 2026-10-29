# Proposal

## Why

V2 已具备独立协议、账号 Actor 和摘要写回，但默认登录尚未实现。先迁移摘要内的登录与角色管理，可在不改变框架边界的情况下验证真实客户端闭环。

## What Changes

- 实现 demo/demo 登录、创建和删除摘要，全部成功响应返回完整可见列表，失败使用统一 ErrorRt。
- 摘要保存删除墓碑，创角不启动角色或地图分片，不创建三表；进入世界时的 lazy 创建留待下一批。
- 提供独立可运行的 V2 验收场景，复用客户端服务、缓存与角色预览，保持默认旧游戏。

## Capabilities

### New Capabilities

### Modified Capabilities

- `server-v2-account`: 演示认证、摘要角色管理、独立客户端验收及后续世界创建边界。
- `server-v2-actor-protocol`: 更新外部投递被拒绝后的登录验收结果，保持内部协议隔离。

## Impact

修改 protov2、Login 模块、协议注册与路由、Session/登录缓存、测试装配和验收场景。不改变 Runtime、连接框架、信封或写回生命周期，不迁移旧存档。
