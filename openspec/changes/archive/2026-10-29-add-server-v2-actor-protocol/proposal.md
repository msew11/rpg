# Proposal

## Why

V2 已有独立邮箱、配置上下文与地图存储，但缺少内部协议的生成边界和 Handler 内跨 Actor 调用方式。需要用已批准的只读对象数量查询验证该边界，保持客户端协议与存档模型独立。

## What Changes

- 内部 proto 按接收方置于 protov2/actor，独立生成到 ActorProtocol/Generated，避免公共协议重复生成。
- ServerModules 实现只读 IActorLocator，Context 必需构造借用 Actors；定位和生命周期由模块所有者维护。
- Role/Async 中注册实际查询处理器，经 Context.Actors.World(MapId) Ask World/Handler，直接交换生成 proto。
- 约定消息所有权移交、同回合等待和禁止自身 Ask/循环等待，保持默认登录尚未实现。
- 验证生成、错误传播、邮箱隔离、多服务隔离和客户端拒绝内部消息，更新文档。

## Capabilities

### New Capabilities

- `server-v2-actor-protocol`: 内部协议生成组织、独占移交和 Role→World 查询示例。

### Modified Capabilities

- `server-v2-runtime`: Context 借用定位接口，业务编排归 Async Handler，模块保有目录生命周期。

## Impact

涉及 rpg.csproj、protov2/actor、src/ServerV2 的生成协议和模块构造链、相关 C# 测试、架构与路线图。无新增依赖；外部协议、默认客户端和存档格式不变，不增加认证、通知框架或跨 Actor 事务。
