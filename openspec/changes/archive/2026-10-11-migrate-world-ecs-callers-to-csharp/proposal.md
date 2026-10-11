## Why

`main` 的 ECS 核心已改为 C#；Godot 不允许 GDScript 继承 C# 脚本，因此 `develop` 的组件和系统必须迁移，世界状态也需使用 C# 类型安全的 ECS API。

## What Changes

- **BREAKING** 将 `scripts/world/` 中的 ECS 组件、系统和世界状态迁移为 C#。
- 保留本地会话、视图、输入、画面工具和手工配置的 GDScript，更新必要的 C# 信号访问及功能测试。
- 保留玩家移动、区块回环、地块副本和变化通知行为；完整项目检查继续通过。
- 更新 `develop` 长期文档和相关 OpenSpec 契约。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-script-boundaries`: 世界状态、组件和 ECS 系统由 C# 实现，保留 GDScript 展示与配置边界。

## Impact

影响 `scripts/world/`、少量 GDScript 视图与功能测试、检查脚本和 `develop` 文档。`scripts/core/`、`tests/core/` 保持与本地 `main` 一致。
