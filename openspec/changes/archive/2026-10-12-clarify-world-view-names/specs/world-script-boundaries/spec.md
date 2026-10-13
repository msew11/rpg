# Spec Delta

## MODIFIED Requirements

### Requirement: 脚本目录反映职责

项目 SHALL 将入口和本地会话放在 `scripts/` 根目录，手工区块配置放在 `scripts/cfg/`，将 C# 世界状态、组件和 ECS System 放在 `scripts/world/`；Godot 展示和交互脚本放在 `scripts/ui/`，无状态展示计算工具放在 `scripts/utils/`。`scripts/core/` 的通用框架代码 SHALL 与 `main` 一致。

#### Scenario: 检查项目脚本

- **WHEN** 开发者浏览 `scripts/` 目录
- **THEN** 世界状态、ECS 组件及 System 使用 C#，入口、配置、UI 与展示计算工具位于各自约定的位置，且项目仍从 `scenes/main.tscn` 启动

#### Scenario: GDScript 视图订阅世界状态

- **WHEN** 玩家、区块或调试状态改变
- **THEN** 保留的 GDScript 视图通过 C# 世界状态的公开成员和信号取得更新

### Requirement: 世界查询不暴露可变组件

世界管理器 SHALL 通过 `chunks_view()` 与 `debug_view()` 让展示层和存档层读取当前区块尺寸、加载范围、地块与调试开关，同时 SHALL 防止调用方通过查询结果改变世界组件。返回的区块列表和地块数组 SHALL 与内部可变数据隔离。

#### Scenario: 展示与存档读取

- **WHEN** 展示层绘制世界，或会话合并活跃区块存档
- **THEN** 调用方取得当前尺寸、加载坐标、地块内容与调试状态，行为与既有场景一致

#### Scenario: 修改查询得到的列表和数组

- **WHEN** 调用方改动读取到的加载坐标列表或地块数组
- **THEN** 后续世界查询及区块行为仍使用原有内部状态，也不产生虚假的变化通知

#### Scenario: 查询调试与区块标量

- **WHEN** 调用方取得区块尺寸、活跃窗口尺寸或调试开关
- **THEN** 查询结果没有可写入口能改变对应世界组件
