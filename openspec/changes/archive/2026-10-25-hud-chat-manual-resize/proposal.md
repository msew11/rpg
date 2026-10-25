## Why
聊天框随技能栏位置和窗口大小变化自动缩放，不符合手动布局预期。需要在 HUD 调整中通过明确的左上角实心三角把手手动改变尺寸，并保持现有保存、退出和恢复的统一语义。
## What Changes
- 聊天固定默认尺寸 320×180 逻辑像素，删除自动避让和自适应尺寸。
- 调整时只有可缩放框左上角绘制实心三角，聊天可从此按住缩放，其他区域继续拖动。
- 完成保存位置和尺寸、退出还原、恢复默认位置与尺寸；尺寸按角色隔离。
## Capabilities
### Modified Capabilities
- `client-ui`: 聊天左上角手动缩放及固定尺寸。
- `client-settings`: 聊天尺寸草稿与角色持久化。
## Impact
GameHud、HudEditOverlay、SettingService、RoleSetting 及真实 UI 回归。保留独立调整操作窗与 ESC 提示。
