# world-script-boundaries Specification

## Purpose

规定当前项目中世界状态、ECS 更新系统、静态计算工具和 UI 脚本的归属，保证单人世界原型的目录语义与运行时职责一致。
## Requirements
### Requirement: 世界规则由无状态 ECS System 更新
项目 SHALL 将世界运行状态保存在 ECS 组件中，并使注册到 ECS 的世界 System 不保存玩家 ID、世界引用或随时间变化的计算缓存。

#### Scenario: 玩家跨越区块边界
- **WHEN** 本地会话在固定步中让玩家跨入相邻行走区块
- **THEN** 区块加载 System 根据当前组件状态更新附近 3 × 3 范围，并保持地块状态与规范区块 ID 对应

### Requirement: 计算工具与 UI 职责分离
项目 SHALL 将坐标和画面几何计算放在顶层 `scripts/utils/` 的无状态静态工具中，具体 UI 脚本负责交互与绘制。

#### Scenario: 雷达和地图刷新
- **WHEN** 玩家移动、区块状态变化或地图被拖动
- **THEN** UI 使用当前世界数据和显示参数计算绘制位置，雷达和地图呈现与既有行为一致

### Requirement: 脚本目录反映职责
项目 SHALL 将入口和本地会话放在 `scripts/` 根目录，手工区块配置放在 `scripts/cfg/`，将 C# 世界状态、组件和 ECS System 放在 `scripts/world/`；视图和无状态展示计算工具保持在原有目录。`scripts/core/` 的通用框架代码 SHALL 与 `main` 一致。

#### Scenario: 检查项目脚本
- **WHEN** 开发者浏览 `scripts/` 目录
- **THEN** 世界状态、ECS 组件及 System 使用 C#，入口、配置、UI 与展示计算工具位于各自约定的位置，且项目仍从 `scenes/main.tscn` 启动

#### Scenario: GDScript 视图订阅世界状态
- **WHEN** 玩家、区块或调试状态改变
- **THEN** 保留的 GDScript 视图通过 C# 世界状态的公开成员和信号取得更新
