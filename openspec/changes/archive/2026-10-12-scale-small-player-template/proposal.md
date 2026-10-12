# Proposal: 调整 16×16 素体的地块比例

## Why

当前 16×16 素体按原始像素尺寸显示，在 100×50 世界像素的等轴地块上过小，玩家截图中难以辨认。

## What Changes

- 将 16×16 素体按整数倍率放大到与当前地块相称的可读大小。
- 保持角色脚部落在玩家世界投影点，维持清晰的像素采样。

## Capabilities

### Modified Capabilities

- `player-visual-template`: 16×16 素体在当前地图上的显示比例。
