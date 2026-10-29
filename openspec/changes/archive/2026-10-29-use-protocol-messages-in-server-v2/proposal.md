# Proposal

## Why

V2 登录协议已经完整表达请求和响应，额外的 DataLogin/DataLoginResult 只增加重复转换。让模块处理器直接使用协议，简化业务扩展入口。

## What Changes

- LoginHandler 直接接收 Login、返回 LoginRt；路由只定位账号 Actor 并返回响应。
- Actor 请求注册支持不实现运行时接口的协议类型；内部请求仍可通过 IActorRequest 推断回复类型。
- 明确可变协议对象的请求独占与移交约定。
- **BREAKING**：移除 DataLogin、DataLoginResult 类型，不改变线上的 V2 协议。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `server-v2-runtime`：明确请求独占协议可直接经过 Actor 邮箱，回复直接返回。

## Impact

涉及 V2 Runtime、登录处理器、默认装配、路由、相关测试和文档。保持默认登录尚未实现、文件存储、会话绑定和旧客户端链路行为。
