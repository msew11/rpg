# Proposal

## Why

Debug 面板目前只创建不可见的 monster ECS 实体，点击后无法从游戏画面确认结果。需要为当前原型补上可见反馈。

## What Changes

- 在主场景为活跃区块的 monster 绘制醒目的临时占位标记，位置对应实体的逻辑世界坐标。
- Debug 创建时仍在当前 chunk 随机选点，但选在玩家附近，使新标记立即出现在镜头中。
- 随区块卸载、重载及会话恢复更新画面；不引入最终美术或 monster 行为。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `debug-monster-spawning`: 增加创建后可见及区块加载生命周期对应的画面要求。

## Impact

涉及 `WorldState` 的 monster 视图读取与创建范围、主场景的 Godot 展示节点、验收测试及路线图状态。
