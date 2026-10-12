# Design

## Context

通用 ECS 位于 `main` 的 `scripts/core/ecs/`，系统已有注册顺序和注册令牌。

## Goals / Non-Goals

**Goals:** 路由按每个 `EcsWorld` 隔离；事件发送同步且可确定顺序；注册阶段验证声明。

**Non-Goals:** 事件队列、异步分发、跨进程传输及持久化。

## Decisions

- 参考 `slime-battle-server` 的 `@EventBus(EventType)`，采用 `[EcsEventHandler(typeof(EventType))]` 显式标记；方法签名为 `void(EcsWorld, EventType)`，使系统不保存世界引用。
- `AddSystem` 一次性反射并校验方法，再添加路由。发送只查精确运行时类型，避免基类订阅产生隐含广播。
- 路由保留注册令牌；分发时使用快照并校验令牌，使发送中移除或重注册系统的行为可预测。

## Risks / Trade-offs

- 反射调用有额外成本 → 注册时缓存方法及路由，不在每次发送时扫描系统。
