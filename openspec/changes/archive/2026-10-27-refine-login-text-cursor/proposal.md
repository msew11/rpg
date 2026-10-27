# Proposal

## Why
Edit 与 Fetch 使用相同缩放比例，但源尺寸不同，文本鼠标仅高 19 像素，明显偏小。登录账号和密码之间的窄缝回落为普通手指，划过时产生突兀切换。

## What Changes
- 文本鼠标独立放大，保持素材比例、最近邻采样与原始热点关系。
- 账号、密码及两者之间的窄缝形成连续文本鼠标区域，离开该区域正常恢复。
- 保持输入框布局、编辑与登录行为。

## Capabilities
### New Capabilities
无。

### Modified Capabilities
- `client-ui`: 调整文本鼠标大小和登录输入区域连续性。

## Impact
PointerCursor、MainController 登录布局分组、素材来源清单、既有指针验收、架构及路线图。
