# Proposal

## Why

世界地图当前每帧重绘数千个菱形瓦片，进入世界后移动仅有约 5 FPS；暂停地图重绘后可达到约 100 FPS。需要让静态地图的绘制结果随相机移动复用，消除持续重绘造成的卡顿。

## What Changes

- 将视口附近地图划为 8×8 格分块，仅绘制新进入的分块并释放离开的分块。
- 静止或在相同分块范围内移动时复用已有绘制结果；窗口尺寸变化时更新分块范围。
- 增加场景验收与 Windows 实机移动性能复测。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-config-map`：可见范围呈现增加分块复用、跨范围更新和地图边缘覆盖场景。

## Impact

修改地图表现节点并新增分块绘制节点；保持地图配置、`MapView.Map`、ECS、输入和 HUD 接口不变。更新客户端架构说明与路线图。
