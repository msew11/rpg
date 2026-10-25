## Context

GameHud.BuildPanels 在角色窗口资料文字下创建“切换角色”按钮。GameHud.ChangeRoleRequested 同时被系统菜单的“返回角色选择”使用，MainController 继续订阅该事件。

## Goals / Non-Goals

**Goals:** 移除角色窗口的重复入口，系统菜单返回角色选择保持可用。

**Non-Goals:** 改动角色资料、系统菜单顺序或切角时的会话清理行为。

## Decisions

- 删除角色窗口按钮的创建和事件绑定，不删除共享的 ChangeRoleRequested 事件。
- 现有系统菜单与角色管理测试继续验证返回流程，UI 边界测试增加角色窗口按钮不存在的断言。

## Risks / Trade-offs

- 返回角色选择需经系统菜单；该入口已有既定位置与验收覆盖。
