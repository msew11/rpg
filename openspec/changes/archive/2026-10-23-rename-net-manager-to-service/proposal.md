# Proposal

## Why

网络通信组件统一使用服务命名，避免 `NetManager` 与后续服务入口的术语不一致。本次仅调整名称，保持现有网络行为和生命周期。

## What Changes

- 将 `NetManager` 类型、构造函数、脚本文件及其 UID 文件统一重命名为 `NetService`。
- 同步运行代码、现有测试、中文注释、诊断信息、架构与路线图中的当前名称。
- 保留命名空间、传输契约、实例创建方式和主控制器的固定帧推进；不引入全局服务入口。
- 历史归档保留原始记录。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。属于纯命名重构，设置 `skip_specs: true`；现有 client-network、client-session 和 session-events 契约继续适用。

## Impact

影响 `src/Net`、`GameSession`、`MainController`、网络和会话测试、角色动画冒烟测试及当前开发文档。无协议、依赖、存档或产品行为变化；内部 C# 调用方同时迁移到新名称。
