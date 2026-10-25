## Context

GameHud 在雷达容器内额外创建“小地图”标签。WindowDragControl 将标题栏设置为 Move，PointerCursor 仅为 Arrow 设置游戏纹理；HUD 调整已有 Fetch 两帧机制。

## Goals / Non-Goals

**Goals:** 去掉普通小地图标题，窗口拖动复用 Fetch，保留 HUD 调整和物品拾取反馈。

**Non-Goals:** 不改窗口拖动保存、HUD 调整名称、快捷键与游戏设计。

## Decisions

- 删除雷达内容标签，保留外框大小和调整框名称。
- WindowDragControl 注册查询组，提供只读抓取状态与精确标题悬停查询；悬停使用实际 GUI 命中，关闭按钮不提示拖动。
- 标题栏保持 Arrow，由 PointerCursor 在 HUD 调整之后、物品拾取之前统一选择 Fetch；抓取查询不依赖当前悬停，因此拖离标题仍握手。不使用系统 Move，避免多套硬件形状覆盖。

## Risks / Trade-offs

- 输入回调先于 GUI 抓取更新 → 逐帧查询最终状态，验收等待真实帧。
- 关闭和失焦残留 → 复用已有 FinishDrag，查询排除隐藏面板，验证恢复。
