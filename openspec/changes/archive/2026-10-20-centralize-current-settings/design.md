# Design

## Context

`MainController` 启动时加载系统设置，选角时加载角色设置，并把两个可变对象传给 `GameHud`。`GameHud` 在事件回调中直接改变对象和调用保存。已有 JSON 读写、默认值修复和文件路径均由 `RepoSettings` 实现。

## Goals / Non-Goals

**Goals:** 当前设置只由 `RepoSettings` 持有；UI 通过只读值和明确的修改方法访问；角色切换重新加载对应角色设置；拖动过程延迟写盘。

**Non-Goals:** 不改变 JSON 文件结构、路径、设置项、界面布局和 Godot 平台效果。

## Decisions

- `RepoSettings` 构造时加载系统设置，`SelectRole(roleId)` 加载当前角色设置。对 UI 暴露标量读取与 HUD 位置副本，不暴露可变设置实例。保留原有 `Load*`/`Save*` 文件接口以供独立读取及兼容测试。
- 分辨率、音量、技能栏显隐和恢复默认布局通过仓库方法更新并立即保存。HUD 拖动通过仓库方法更新内存位置，在松开或退出调整模式时保存当前角色。这样 UI 不直接修改 DTO，且拖动期间不逐帧写盘。
- Godot 窗口、音量总线和控件布局仍由 UI 应用；仓库只管理本地数据。拆分数据文件时 `DataHudPosition` 保持顶层类型，与 `DataRoleSetting` 同文件，保持引用和 JSON 结构。

## Risks / Trade-offs

- 仓库同时负责磁盘数据和当前选择状态，实例须按客户端生命周期复用。主控制器在启动时创建一次，并在每次选角时更新当前角色。
- HUD 拖动结束前关闭界面可能丢失内存中的位置。退出调整模式和界面树时保存待提交的拖动位置。
