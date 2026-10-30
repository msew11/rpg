# Proposal

## Why

当前物品与世界同步处理器同时实现多个 IActorHandler，且部分纯本地消息混在 Async 目录，无法直接按消息找到唯一职责的实现。按用户要求收紧处理器结构，保持现有业务行为。

## What Changes

- 每个业务 Handler 只实现一个 IActorHandler，并使用消息名加 Handler 的类型与文件名。
- 跨 Actor 编排归所属模块 Async，本地处理归 Handler，Handler 目录只放 Handler；跨 Actor 共用逻辑归 Common/Utils，Actor 内共用逻辑归各自模块 Utils。
- 更新显式注册及既有 AOI 验收调用，补充结构约束回归。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `server-runtime`：规定业务消息处理器的单消息职责、文件命名和 Async/Handler 目录边界。

## Impact

涉及 src/Server 的 Login、Role、World 处理器、注册入口及测试。协议、持久化格式、游戏行为和 Actor 调度保持现状。
