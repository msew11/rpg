# Proposal

## Why

移动输入已通过 `LocalSession`，但地块快捷键、调试开关和怪物创建仍由 `Main` 或 `DebugPanel` 直接向世界发送事件。同一运行会话的交互入口因此分散，后续替换会话实现时难以确定调用边界。

## What Changes

- 本地会话提供移动、地块、调试开关和测试怪物创建的交互方法，并返回世界命令结果。
- `Main` 与 `DebugPanel` 经当前会话提交交互；视图继续读取世界状态与信号。
- 地图拖动、显示开关、镜头缩放等纯 UI 行为继续由对应 UI 节点处理。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-interaction-events`：当前会话成为游戏输入与调试操作的统一提交入口。

## Impact

影响 `scripts/local_session.gd`、`scripts/main.gd`、`scripts/view/debug_panel.gd`、交互回归测试及架构、路线图文档。世界事件和存档格式保持不变。
