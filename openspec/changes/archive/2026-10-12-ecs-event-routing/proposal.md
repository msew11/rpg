# Proposal

## Why

未来的世界交互会持续增加，ECS 需要按事件类型把交互交给系统处理，避免世界入口逐项实现玩法规则。

## What Changes

- 为 ECS 增加事件基类、方法注解、注册时路由构建及同步发送入口。
- 系统注销时移除路由；无处理者事件返回失败，错误处理声明在注册时拒绝。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `ecs-runtime`: 增加事件注册和分发契约。

## Impact

仅修改 `main` 归属的 `scripts/core/ecs/`、`tests/core/`、`docs/ecs.md` 和 ECS 规格。具体游戏事件由 `develop` 使用。
