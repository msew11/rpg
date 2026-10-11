## MODIFIED Requirements

### Requirement: 脚本目录反映职责
项目 SHALL 将入口和本地会话放在 `scripts/` 根目录，手工区块配置放在 `scripts/cfg/`，将 C# 世界状态、组件和 ECS System 放在 `scripts/world/`；视图和无状态展示计算工具保持在原有目录。`scripts/core/` 的通用框架代码 SHALL 与 `main` 一致。

#### Scenario: 检查项目脚本
- **WHEN** 开发者浏览 `scripts/` 目录
- **THEN** 世界状态、ECS 组件及 System 使用 C#，入口、配置、UI 与展示计算工具位于各自约定的位置，且项目仍从 `scenes/main.tscn` 启动

#### Scenario: GDScript 视图订阅世界状态
- **WHEN** 玩家、区块或调试状态改变
- **THEN** 保留的 GDScript 视图通过 C# 世界状态的公开成员和信号取得更新
