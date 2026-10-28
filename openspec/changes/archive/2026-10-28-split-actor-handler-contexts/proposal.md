# Proposal

## Why

当前所有 Handler 使用同一 ServerRequestContext，登录、角色与地图权限没有由类型表达，地图 Handler 还由 RoleActor 声明并以委托转发。按 Actor 拆分声明与上下文，明确每类 Handler 可以操作的缓存。

## What Changes

- LoginActor、RoleActor、WorldActor 各自声明并复用所属请求 Handler。
- 引入 LoginContext、RoleContext、WorldContext，每种 Context 以 EntityCache 持有对应缓存；世界事务只接收角色工作副本。
- Handler 目录按所属 Actor 组织；世界请求直接由 WorldActor 路由，不从角色 Actor 传递 Handler 委托。
- 移除通用 ServerRequestContext 和通过布尔值切换 Context 的工作消息。

## Capabilities

### New Capabilities
无。

### Modified Capabilities
无。保持 server-actors 与 mock-server 既有可观察行为，本次仅调整内部处理契约，使用 skip_specs。

## Impact

影响服务端 Actor、Handler、Context、内部工作消息、纯内存验收和架构文档。协议、存档和游戏规则保持现有契约。
