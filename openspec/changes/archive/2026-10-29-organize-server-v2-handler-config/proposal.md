# Proposal

## Why

业务处理器应集中放在各模块的 Handler 目录，配置访问也应有统一入口，避免每个 Handler 重复持有 ConfigService。

## What Changes

- 将 V2 业务 Handler 移入所属模块的 Handler 子目录。
- Login、Role、World 的 Context 统一提供只读 Config 属性，借用所属服务注入的配置。
- LoginHandler 移除配置字段和构造参数，继续直接处理协议。
- 验证不同服务的配置隔离与借用生命周期，更新文档。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `server-v2-account`：明确业务通过所属上下文访问服务配置，Handler 不持有配置依赖。

## Impact

调整 V2 模块装配、Actor/Context、登录 Handler 目录、相关测试和文档。运行时邮箱、存档、旧 Server 及默认未实现业务行为不变。
