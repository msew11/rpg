## ADDED Requirements

### Requirement: GM 逻辑 chunk 边界控件
GM 面板 SHALL 在“显示物体锚点”下方提供 chunk 边缘显示开关和颜色按钮。边界默认关闭、颜色默认绿色；切换和改色 SHALL 即时更新当前地图，且不修改地图数据。物体锚点与 chunk 两行的开关 SHALL 与控制台开关右侧对齐，各自颜色按钮 SHALL 位于开关左侧。颜色选择面板 SHALL 使用紧凑尺寸。

#### Scenario: 开启、改色和关闭边界
- **WHEN** 玩家打开 GM，开启 chunk 边缘、选择另一颜色后关闭
- **THEN** 地图逻辑 chunk 边缘先显示绿色细线，再即时改色，最后隐藏

#### Scenario: GM 控件布局
- **WHEN** 玩家打开 GM 面板
- **THEN** 两个调试开关与控制台开关右侧对齐，颜色按钮紧靠各自开关左侧，颜色面板比原布局更紧凑
