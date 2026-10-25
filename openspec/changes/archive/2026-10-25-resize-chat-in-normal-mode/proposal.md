## Why

用户要求聊天右上角缩放直接在常态下使用，不再作为 HUD 调整模式功能；保存时机与普通窗口位置调整一致。

## What Changes

- 常态显示右上角缩放三角，固定左下角改变大小，松开或失焦提交位置与尺寸。
- HUD 调整只移动聊天，不提供缩放入口；保留布局保存、取消及恢复默认。
- 尺寸继续按角色保存，重选和重启恢复。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 常态聊天缩放入口与输入隔离。
- `client-settings`: 常态缩放位置、尺寸即时保存契约。

## Impact

GameHud、HudEditOverlay、PointerCursor、新增聊天缩放控件及既有 BagGmSmoke；沿用当前设置格式。
