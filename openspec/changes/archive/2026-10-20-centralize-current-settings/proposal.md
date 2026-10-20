# Proposal

## Why

系统和角色设置的可变实例目前由多个 UI 类持有，修改和保存分布在界面事件中，角色切换时也需要手工传递设置实例。把当前设置的生命周期和写入入口集中到仓库，降低界面与持久化细节的耦合。

## What Changes

- `RepoSettings` 持有当前系统设置和选中角色设置，提供明确的修改、保存及角色切换操作。
- UI 只读取当前设置并调用修改操作；保留窗口、声音和 HUD 的界面效果。
- 将 `DataSettings.cs` 拆成 `DataSystemSetting.cs` 与 `DataRoleSetting.cs`，后者同文件保留 `DataHudPosition`。
- 保持 JSON 格式、设置路径、默认值、用户交互和现有验收行为。

## Capabilities

没有新增或修改对外行为；这是 `client-settings` 的内部重构，故跳过 delta spec。

## Impact

涉及 `src/Settings/`、`src/UI/MainController.cs`、`src/UI/GameHud.cs` 和设置相关测试；不增加依赖。
