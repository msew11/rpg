# Design

## Context
PointerCursor 只注册 Arrow；原生 LineEdit 在文本区自动选择 Ibeam，因此仍显示系统纹理。既有导入工具可完整解码同目录的 Edit.gsa，原 uiroot.lua 明确配置 EditCursor 首帧及热点 (5,8)。

## Goals / Non-Goals
统一原生文本鼠标的素材与缩放，不改控件、文字插入光标或输入协议。

## Decisions
- 在同一导入工具增加 edit.png 与哈希、帧数、热点记录，保持运行时只读项目资源。
- 复用 LoadFrame 的最近邻缩放，将 Edit 注册到 Input.CursorShape.Ibeam，交由引擎按真实悬停选择。不逐个改登录控件或模拟悬停，避免密码框、选字及其他输入框行为分叉。
- 退出时同时清除 Arrow 与 Ibeam；普通 Click 动画继续更新 Arrow，不覆盖 Ibeam 槽位。

## Risks / Trade-offs
- 硬件鼠标像素无法从 headless 截图验证 → 检查素材、注册逻辑并在既有 smoke 中验证真实输入与形状切换，实际桌面观感保留图形窗口验收。
