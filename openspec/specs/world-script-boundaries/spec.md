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

### Requirement: 世界交互由事件系统承接

世界管理器 SHALL 将交互事件转发给 ECS；具体移动意图、地块、调试开关和怪物生成规则 SHALL 位于世界 System 中，避免管理器按交互类型扩展方法。

#### Scenario: 检查新增交互位置
- **WHEN** 开发者查看交互命令实现
- **THEN** 世界管理器仅执行事件转发和变化通知，具体规则可在对应 System 中找到

### Requirement: 世界管理器封闭裸 ECS 入口

世界管理器 SHALL 自行持有和调度 ECS，生产调用方 SHALL 无法通过世界管理器取得可直接增删实体、组件或 System 的 ECS 实例。现有交互命令、世界读取和存档行为 SHALL 保持可用。

#### Scenario: 生产调用方检查世界入口

- **WHEN** 生产调用方使用世界管理器的公开接口
- **THEN** 无法从该接口取得可直接修改实体、组件或 System 的 ECS 实例

#### Scenario: 实体随区块保存和恢复

- **WHEN** 区块内实体在保存、卸载和重新加载后被检查
- **THEN** 实体的父子关系和持久组件仍按既有规则恢复
