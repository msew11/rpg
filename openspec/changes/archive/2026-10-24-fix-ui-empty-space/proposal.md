## Why

HUD 头像、技能栏、菜单及背包存在与内容无关的大块留白，遮挡世界画面。用户要求消除这些多余区域。

## What Changes

- HUD 头像、技能栏和菜单按实际内容尺寸布局，调整模式显示把手时重新收紧并约束位置。
- 背包按完整网格及标题计算尺寸，小窗口仍支持双向滚动。
- 验证普通及小窗口、HUD 调整和内容刷新后的实际布局。
- 默认聊天、首条技能栏与菜单底边统一距离视口底部 16 像素，保留自定义 HUD 位置。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 增加 HUD 与背包内容贴合的布局要求。

## Impact

影响 GameHud、BagControl 及现有背包场景验收；不涉及协议、存档或玩法决策。
