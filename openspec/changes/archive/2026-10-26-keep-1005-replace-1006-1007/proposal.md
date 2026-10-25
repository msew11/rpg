# Proposal

## Why

上一轮将 1005 与 1006 合并到 1003，是对用户音效编号的误解。实际要求是保留登录与进入游戏成功时的 1005，并把原先使用 1006、1007 的操作统一改为 1003，避免界面继续播放错误的反馈音效。

## What Changes

- 登录与进入游戏成功后恢复播放 1005，失败仍不播放成功反馈。
- 原 1006 和 1007 的按钮、菜单收起及 HUD 遮罩/Esc 操作统一播放 1003。
- 播放器恢复加载 1005，停止加载 1007；物品交互音效保持不变。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 修正登录成功、确认和取消关闭操作的音效契约。

## Impact

涉及 `MainController`、`GameHud`、`UiSoundPlayer`、音效回归测试及客户端规格和文档；不删除原始 WAV 资源。
