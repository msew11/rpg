# Proposal

## Why
登录输入框悬停时仍使用系统 I 形鼠标，与现有 Normal、Click 和 Fetch 游戏素材不一致。原素材库提供专用 Edit.gsa，可复用现有导入链路恢复统一外观。

## What Changes
- 导入 Edit.gsa 首帧及原始热点 (5,8)，记录来源和哈希。
- 按既有 1.25 倍最近邻缩放注册 Godot Ibeam，登录账号、密码等原生文本输入区域共用；离开主场景撤销注册。
- 验证输入框悬停、点击编辑、移出恢复与既有指针反馈。

## Capabilities
### New Capabilities
无。

### Modified Capabilities
- `client-ui`: 文本输入区域使用配套 Edit 鼠标素材。

## Impact
PointerCursor、指针导入工具与资源、现有 PointerFeedbackSmoke、架构及路线图。不改变输入框行为或登录协议。
