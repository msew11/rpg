# Proposal

## Why

当前玩家仍以箭头占位显示，无法在等轴世界中直接检查新取得的 ErisEsra 素体比例、方向和行走动画。需要一个运行时切换入口来比较两种角色模板。

## What Changes

- 接入 ErisEsra 16×16 与 16×32 两套角色模板的待机、行走精灵表，以脚部为锚点按玩家朝向显示。
- 在 Debug 面板提供占位角色、16×16、16×32 三个选项，切换后立即更新玩家视图。
- 选择仅作为本地调试显示状态，重建玩家视图后保持选择；玩家位置和朝向仍来自当前会话。
- 记录素材来源、使用条件和动作缺口。

## Capabilities

### New Capabilities

- `player-visual-template`: 玩家显示模板选择及八方向待机、行走动画。

### Modified Capabilities

无。

## Impact

影响 `PlayerView`、`Main`、`DebugPanel`、主场景 UI、玩家运行时 PNG 资源与相关展示测试。不会改变 ECS 移动、存档格式或怪物显示。
