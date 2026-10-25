## Why
HUD 调整缺少取消入口且拖动立即保存，小地图和聊天无法调整。

## What Changes
- 小地图、聊天纳入角色 HUD 布局。
- 完成保存全部位置，退出丢弃草稿并还原进入前位置。
- 调整中恢复默认仅更新草稿，非调整时直接保存默认布局。

## Capabilities
### Modified Capabilities
- `client-settings`: HUD 草稿、保存、取消和恢复默认。

## Impact
GameHud、ChatControl、角色布局默认值、UI 回归测试与架构路线图。
