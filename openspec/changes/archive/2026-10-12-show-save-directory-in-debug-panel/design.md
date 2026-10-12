# Design

## Context

`SaveSlots.save_dir` 是正式槽位的目录，默认 `user://saves`；`WorkingSaveStore` 的工作文件默认也位于其中。Debug 面板当前固定在雷达上方。

## Goals / Non-Goals

**Goals:** 以当前存档管理器的目录为唯一来源，展示用户可在 Windows 中查找的路径。

**Non-Goals:** 不迁移存档、不修改槽位文件名或写入流程。

## Decisions

- 主入口通过 `ProjectSettings.globalize_path(save_slots.save_dir)` 得到绝对路径并传给 Debug 面板；避免面板直接依赖存档实现。
- 使用只读单行文本框显示完整路径，可水平滚动、选中和复制。扩大面板高度并下移雷达，防止覆盖。
- 只读文本框获得焦点时仍允许空格切换地块；现有输入保护只跳过可编辑文本框。

## Risks / Trade-offs

- 绝对目录可能较长，文本框在固定宽度内水平滚动，完整路径可通过选中复制获取。
