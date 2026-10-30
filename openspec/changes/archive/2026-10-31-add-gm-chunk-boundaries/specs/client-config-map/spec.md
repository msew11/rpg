## ADDED Requirements

### Requirement: 地图逻辑 chunk 调试边界
地图表现 SHALL 按当前地图配置的 `chunkSizeTiles` 绘制可见逻辑 chunk 的等轴细边框，在地图宽高处截断。绘制层 SHALL 覆盖地表且不影响地图静态分块复用或实体坐标。

#### Scenario: 视口移动到地图边缘
- **WHEN** GM 边界开启，视口跨越 chunk 或移动到地图边缘
- **THEN** 当前可见 chunk 的边框随视口更新，最后一个 chunk 的边框止于地图边缘
