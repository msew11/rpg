## Why

目前地面点击光圈仅淡出，缺少用户要求的由小到大变化。增加以点击位置为中心的扩散动画，让蓝色椭圆光圈产生一次涟漪，同时保留现有输入隔离与自动清理。

## What Changes

- 光圈从原尺寸的 25% 放大到 125%，在 0.5 秒内同时淡出。
- 更新场景验收，验证扩散过程保持中心与椭圆比例、持续变大并释放。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 地面点击光圈由静态淡出改为扩散涟漪。

## Impact

GroundClickView、现有点击反馈场景验收及相关文档。不修改原始素材、普通指针或移动规则。
