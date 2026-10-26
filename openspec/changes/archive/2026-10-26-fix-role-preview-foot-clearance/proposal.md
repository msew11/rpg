# Proposal

## Why

登录后的选角页与创角页人物脚部过于贴近旋转按钮，影响完整展示。用户要求适当缩小人物，让脚部和操作区分开。

## What Changes

- 两个全屏角色页面的人物预览由 2.4 倍缩小到 2 倍，并增加脚底定位点到预览区底边的距离。
- 验证男女八方向站立时头身完整、脚部与旋转按钮保持间距。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `role-management`：明确选角与创角预览脚部和旋转操作区之间的可见间距。

## Impact

涉及 RoleScreenControl、RolePreviewControl、角色界面验收及路线图。不改变装备窗口预览比例、协议、世界角色或外观资源。
