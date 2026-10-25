## Context

五个普通窗口通过 GameHud.CreatePanel 创建 ViewportPanelControl，背包由 BagControl 单独创建。ViewportPanelControl 同时用于 HUD 调整窗口；聊天、小地图、角色信息使用其他控件。两种目标面板都通过 PanelContainer 的 `panel` StyleBox 计算外框尺寸。

## Goals / Non-Goals

**Goals:** 六个指定窗口的标题栏和内容距离四边至少 12 个逻辑像素；保持可见内容、滚动访问与现有背景。HUD 保持现状。

**Non-Goals:** 改动 HUD 调整窗口、道具图鉴、背包容量或窗口拖动设置。

## Decisions

- 提供一个窗口样式 helper，复制面板当前 StyleBox，再将四边内容边距提升到至少 12 像素；复制避免修改共享主题。
- GameHud.CreatePanel 创建的五个窗口显式启用该样式；BagControl 使用同一个 helper。HUD 调整窗口不启用。
- 保持原有按 StyleBox 最小尺寸计算自然窗口大小的逻辑，使新增边距纳入视口裁剪和滚动判断。

## Risks / Trade-offs

- 窄视口可用于内容的空间减少；依赖已有滚动容器保证内容和关闭按钮可访问。
- 背包外框扩大 24 个逻辑像素；网格尺寸与物品交互不变。
