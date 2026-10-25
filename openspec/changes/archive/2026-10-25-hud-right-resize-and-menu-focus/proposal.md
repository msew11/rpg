## Why
聊天缩放应从右上角操作，当前左上角把手与预期不一致。系统菜单展开时主动聚焦首项，导致未悬停或选择时“系统设置”默认高亮，需修正该表现。
## What Changes
- 缩放三角与命中区移到右上角，固定左下角缩放，名称仍在左上角。
- 系统菜单展开不再主动聚焦首项，保留鼠标悬停及键盘导航。
## Capabilities
### Modified Capabilities
- `client-ui`: 右上角手动缩放与菜单初始焦点。
## Impact
GameHud、HudEditOverlay、真实输入验收与文档，保存取消规则不变。
