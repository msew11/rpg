## Context

ApplySystemSettings 仅在启动调用，先读取已保存分辨率并通过 DisplayServer.WindowSetSize 应用。用户要求启动窗口居中。

## Goals / Non-Goals

在最终启动尺寸确定后居中窗口；不增加窗口位置存档或改变游戏内分辨率设置流程。

## Decisions

在 WindowSetSize 后调用 GetWindow().MoveToCenter()，复用 Godot 4.7.2 的当前显示器居中能力，避免自行计算多显示器坐标和边框。嵌入运行时由引擎在宿主视口内居中。

## Risks / Trade-offs

headless 检查只能验证构建与启动，真实桌面位置需要 Windows 图形窗口验收；嵌入模式的宿主外层窗口由编辑器控制。
