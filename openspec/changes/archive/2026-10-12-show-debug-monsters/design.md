# Design

## Context

当前 monster 只有区块子实体、身份标记和连续世界位置；`WorldState` 在区块加载变化时发出 `chunks_changed`。主场景以 Godot 节点绘制地面和玩家。

## Goals / Non-Goals

**Goals:** 在当前镜头中立即看见新建 monster，并在区块加载与回环时保持标记位置正确。

**Non-Goals:** 最终怪物美术、动画、交互、AI 或战斗。

## Decisions

- 创建时从玩家所在 tile 附近、同一行走区块内的其他 tile 随机选点。该范围在现有相机缩放下可见，同时避免与玩家标记完全重叠。
- `WorldState` 只向视图提供活跃行走区块内的 monster 位置；同一规范区块在回环中出现多个行走实例时，按各实例相对原始位置平移，不改变实体的持久位置。
- 新增独立的 `MonsterLayer` 绘制明亮的占位图形；它响应 `chunks_changed` 重绘。创建实体后也发送该通知。该节点位于地面之上、玩家之下，不把图形状态放入 ECS。

## Risks / Trade-offs

- [临时图形缺少最终角色细节] → 仅作为 Debug 可见反馈；后续美术需求再替换。
- [同一规范区块的多个行走实例重复显示同一 monster] → 与现有回环地块一致，仅呈现当前活跃实例，不复制 ECS 实体。
