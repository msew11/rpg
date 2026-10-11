## Why

Rider 的移动场景快照显示，Godot 调用 C# 的桥接和 Variant 返回值转换累计耗时明显高于区块加载计算。雷达在移动时持续重绘，而几何工具为每个区块顶点反复读取同一玩家和区块状态，造成不必要的跨语言调用。

## What Changes

- 每次雷达重绘集中读取玩家位置、朝向和区块显示状态，并用这一份状态计算全部雷达几何。
- 保留当前区块、蓝色加载区、灰色网格、回环接缝及现有测试调用的可见行为。
- 对比优化前后的桥接调用次数，并验证跨普通、负坐标及回环边界时的雷达结果。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `continuous-chunk-world`：增加雷达重绘时对世界状态读取次数的约束，保留现有可见行为。

## Impact

主要涉及 `scripts/view/chunk_radar.gd`、`scripts/utils/util_radar_projection.gd` 及对应功能测试。不修改 ECS 核心、世界规则、地图素材或存档格式。
