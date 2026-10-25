## Context
GameHud 已持有独立布局草稿和绿色覆盖层，ViewportPanelControl 提供关闭、居中及小窗口边界。

## Goals / Non-Goals
将调整操作移到独立窗口；不修改保存、取消、恢复默认和抓取行为。

## Decisions
复用 ViewportPanelControl 建立 HudEditWindow，内容按钮依次为完成、退出、恢复。进入时隐藏系统设置和其他普通面板，显示操作窗；关闭只调用 Hide。Esc 仅在调整中重新 Show 并居中，不切换系统设置或退出模式。顶部居中 Label 提示持续到完成或退出，忽略鼠标。完成提交草稿，退出丢弃，恢复只重置草稿；系统设置中非调整的恢复默认布局继续立即保存。

## Risks / Trade-offs
操作窗保持非模态，便于在未被遮挡的 HUD 上拖动；小窗口沿用现有滚动和视口限制。验证真实关闭、Esc 呼出后草稿不变以及提示显隐。
