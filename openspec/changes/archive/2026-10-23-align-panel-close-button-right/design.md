# Design

## Context

所有游戏面板通过 `GameHud.CreatePanel` 创建共用横向标题栏，标题和关闭按钮目前按最小宽度相邻排列。

## Goals / Non-Goals

**Goals:** 共用布局统一右侧关闭位置，并随面板宽度自适应。

**Non-Goals:** 不改变关闭逻辑、按钮文字或面板内容。

## Decisions

将标题 Label 的水平尺寸标志设置为 ExpandFill，占据关闭按钮以外的可用空间。相比固定坐标或固定间距，这种方式适配各面板的实际宽度。

## Risks / Trade-offs

- [标题栏布局影响面板显示] → 运行项目检查及现有客户端场景冒烟测试，确认面板创建和设置流程正常。
