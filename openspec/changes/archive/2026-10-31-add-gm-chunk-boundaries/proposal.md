# Proposal

## Why
GM 当前只能看到实体锚点，无法在地图上确认逻辑 chunk 边界；两个调试控件的布局也占用过多空间。

## What Changes
- 在地图上为当前可见的逻辑 chunk 绘制可开关的细边框，默认绿色，可即时改色。
- 在 GM 面板中将 chunk 控件放在物体锚点下方，两个颜色按钮都位于右侧开关左边，并缩小颜色面板。

## Capabilities
### Modified Capabilities
- `client-ui`: GM 调试控件与即时反馈。
- `client-config-map`: 按地图配置呈现 chunk 边界。

## Impact
影响 GM 面板、地图表现和对应 Godot 验收；不修改地图数据或业务坐标。
