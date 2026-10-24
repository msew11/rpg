## Context

GM 原有设置路径行来自 SettingService，但用户需要打开包含设置和模拟服务端存档的用户数据根目录。验收目标为直接看到两个操作，目录不含机器相关硬编码，图鉴仍通过原入口打开。

## Goals / Non-Goals

目标：GM 内容仅保留两个同级平铺按钮，按顺序打开用户数据目录、查看道具图鉴；保留关闭及小窗口边界行为。
非目标：不调整存储布局、图鉴数据或聊天 GM。

## Decisions

- 使用 HBoxContainer 并排排列等宽按钮，复用现有弹窗滚动与视口约束。
- 点击时通过 ProjectSettings.GlobalizePath("user://") 解析当前应用的用户数据目录，再调用 OS.ShellShowInFileManager 并打开目录内内容。
- 使用“用户数据目录”作为界面术语，提示其中存放本地设置和模拟服务端存档。失败通过既有错误标签反馈。

## Risks / Trade-offs

操作系统文件管理器属于外部界面，headless 验收不启动它；通过代码核对目录解析及打开语义，并运行现有 GM 图鉴与窗口边界验收。
