# Proposal

## Why

世界入口原先为每种输入增加一个方法，交互增加时会持续扩张。把输入表示为事件并交给 ECS System，可让入口保持稳定，同时让规则与数据更接近。

## What Changes

- 将 `WorldState` 更名为 `WorldManager`，更新项目中的类型引用。
- 让移动、地块切换、调试开关和调试怪物创建通过 `send_event()` 进入 ECS。
- 保留现有物理步、世界数据读取和视图通知行为。

## Capabilities

### New Capabilities

- `world-interaction-events`: 定义游戏交互事件入口和现有命令行为。

### Modified Capabilities

- `session-data-repository`: 世界管理器名称与创建接口更名。
- `world-script-boundaries`: 世界交互由带注解的 System 处理。

## Impact

更新 `scripts/world/`、入口、会话、视图、测试及相关开发文档；不新增玩法、网络协议或存档字段。
