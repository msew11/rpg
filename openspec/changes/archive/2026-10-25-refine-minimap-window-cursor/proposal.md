## Why

小地图上方的标题占用空间，普通窗口拖动的系统十字鼠标与游戏 Fetch 反馈不一致。按用户要求简化小地图并统一拖动反馈。

## What Changes

- 移除普通显示时的小地图标题，保留 HUD 调整框内名称。
- 可拖动窗口标题栏悬停显示 Fetch 开手，抓取显示握手，释放、失焦、关闭后正确恢复。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 小地图普通显示不带标题。
- `client-settings`: 窗口标题拖动使用游戏 Fetch 指针。

## Impact

修改 GameHud、WindowDragControl、PointerCursor 及现有角色设置场景验收；不改协议与设置文件格式。
