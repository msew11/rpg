# Proposal

## Why

`send_event()` 目前只返回是否找到处理方法；即使地块未加载或设置值未变，也返回 `true`。调用方无法判断命令是否真正生效。

## What Changes

- **BREAKING** 将世界事件入口的布尔返回值替换为只读结果，分别表达无处理者、已处理但未变化、已改变。
- 将移动意图变化计入命令结果，同时保留玩家视图在世界推进后才收到位置或朝向通知的时序。
- 为三类结果和现有交互增加回归测试，说明结果与视图通知的关系。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-interaction-events`：世界事件调用方可区分路由与执行后的状态变化。

## Impact

影响 `scripts/world/` 的世界入口及结果类型、依赖旧布尔返回值的测试、架构与路线图。通用 `scripts/core/` 的事件路由契约保持不变；不改变存档格式和游戏交互。
