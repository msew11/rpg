# Proposal

## Why
MockServerService 目前由 Net 隐式创建并释放，与其他 Service 的统一装配及生命周期约定不一致。用户要求取消不增加全局服务的限制，并统一检查 Service 依赖使用非空类型。

## What Changes
- GameServices 显式创建、注册和逆序释放 MockServerService，公开 MockServer 只读入口。
- Net 构造接收现成服务，只管理连接；内部进程内回归显式选择传输。
- 检查全部 Service 构造参数及字段，移除可空 Service 依赖并拒绝空值；尚未启动的普通 MockServer 保持可空。

## Capabilities
### New Capabilities
无。
### Modified Capabilities
- `game-services`: 统一服务器服务入口、依赖非空及生命周期。
- `server-v2-account`: Net 借用服务，所有者统一释放。

## Impact
GameServices、Net、TCP及进程内适配、LogService、其他服务构造校验、服务测试和架构文档；不改变协议与登录时才启动服务器的行为。
