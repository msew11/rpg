## Why

现有显示设置只列出三个分辨率，且无法在窗口、独占全屏和无边框窗口全屏之间切换。

## What Changes

- 显示页提供覆盖常见 4:3、16:10、16:9 尺寸的分辨率选项，并保留旧存档中的自定义尺寸。
- 增加窗口、全屏、无边框窗口全屏三种模式，应用后立即生效并随系统设置保存。
- 分辨率作为普通窗口尺寸保存；全屏时使用显示器尺寸，返回窗口时恢复保存尺寸并居中。

## Capabilities

### Modified Capabilities

- `client-settings`: 系统显示设置的模式、尺寸和重启恢复。

## Impact

SettingService、SystemSetting、GameHud、MainController 和显示设置应用工具；不改变角色设置和服务器协议。
