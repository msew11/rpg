# Proposal

## Why

GM 已能看到地图逻辑 chunk 边界，却无法直接区分当前客户端实际订阅的 AOI 块。调试跨块移动和地图边缘时，需要把服务端下发并接纳的范围直观标出。

## What Changes

- GM 面板增加“显示当前 AOI”开关，默认关闭。
- 开启后用半透明青蓝色高亮已接纳的 AOI 块，中心块颜色更深、边线更粗；地图边缘按实际块范围截断。
- 高亮在绿色逻辑 chunk 线之上、粉色绘制区域线之下；范围随权威 AOI 快照更新，离开世界时清除。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`：增加基于权威 AOI 缓存的 GM 范围调试显示。

## Impact

涉及 GM 面板、世界地图覆盖层和客户端地图快照读取；不增加协议字段、不修改服务器 AOI 计算或地图存档。
