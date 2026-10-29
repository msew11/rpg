# Proposal

## Why

现有 MockServer 集中管理连接、路由、分片、业务和存档协调，难以清楚扩展。建立独立 V2 框架，保留串行邮箱和状态独占的 Actor 思想，并先确定账号与连接边界。

## What Changes

- 在 src/ServerV2 从零实现宿主、连接会话、协议适配、Actor 运行时和 Login/Role/World 模块边界；旧 Server 与客户端继续运行。
- 新增 protov2，落实四字段信封、统一错误、登录及角色摘要协议，不实现真实认证或游戏业务。
- LoginActor 按规范 username 分片；连接登录成功后保存账号键，后续账号消息从 Session 路由。
- 明确 username.role_summary.json 保存该账号全部摘要的存储契约，不实现文件读写和旧存档迁移。
- 增加真实 protobuf 字节链路、并发隔离与生命周期验收，并记录模块接入方法。

## Capabilities

### New Capabilities

- `server-v2-runtime`: V2 Actor 邮箱、类型化消息、身份分片和宿主停止。
- `server-v2-network`: 连接绑定会话、四字段信封、错误与过期消息隔离。
- `server-v2-account`: username 规范化、账号路由及摘要存储契约。

### Modified Capabilities

- `game-services`: 在顶层固定服务组之外，明确 V2 MockServerService 由 Net 拥有且借用配置的子服务依赖；默认旧链路不切换。

## Impact

新增 src/ServerV2、src/ProtocolV2、protov2 及 C# 验收；更新 rpg.csproj 的独立 protobuf 生成项、架构与路线图。复用现有 .NET、protobuf 和测试依赖，不切换客户端入口。
