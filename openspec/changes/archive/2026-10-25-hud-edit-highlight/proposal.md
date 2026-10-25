## Why
当前调整态增加标题拖动条，会改变 HUD 高度与默认位置，而且只能从标题开始拖动。需要在不改变原布局的情况下清楚显示可调整范围，并用 Fetch 手形反馈悬停和抓取。
## What Changes
- 绿色边框和框内左上角名称标签，整块区域可拖动。
- Fetch 首帧悬停，第二帧按住，释放和退出恢复正确指针。
## Capabilities
### Modified Capabilities
- `client-ui`: HUD 调整覆盖层与抓取鼠标。
## Impact
GameHud、PointerCursor、指针素材导入、HUD 回归验收与文档。
