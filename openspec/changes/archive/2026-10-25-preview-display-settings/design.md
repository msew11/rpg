## Context

GameHud 目前在“应用并保存”按钮中同时调用 SettingService.SetDisplay 和 DisplaySettings.Apply。系统设置面板可由关闭按钮、其他普通面板切换或退出世界收起。

## Decisions

- 显示页局部保存最近提交的窗口模式和普通窗口尺寸。选项事件只调用 DisplaySettings.Apply；按钮改为“保存”，成功写盘后更新回退基线。
- 系统设置面板隐藏时撤销未保存预览并重置控件。ReleaseSubscriptions 在离开世界时执行同一回退，且只执行一次。
- SetDisplay 先写候选设置，成功后才替换内存值；写盘失败保留原设置，使面板关闭能可靠还原。
- 全屏下拉框仍只读显示当前显示器分辨率；普通窗口尺寸在预览全屏期间保留。

## Risks / Trade-offs

- Godot headless 不提供真实全屏窗口。场景验收覆盖窗口尺寸、控件、持久化和回退；真实全屏切换仍需 Windows 图形会话验证。
