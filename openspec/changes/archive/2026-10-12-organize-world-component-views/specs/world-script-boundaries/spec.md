## ADDED Requirements

### Requirement: 世界组件只读视图集中放置

项目 SHALL 将 `WorldChunksView` 与 `WorldDebugView` 及其 Godot UID 放在 `scripts/world/compviews/`，并保持现有只读查询接口与隔离行为。

#### Scenario: 检查组件视图目录

- **WHEN** 开发者查看 `scripts/world/compviews/`
- **THEN** 两个只读组件视图及其 UID 位于该目录，世界组件仍位于 `scripts/world/components/`

#### Scenario: 移动后读取世界视图

- **WHEN** 展示层或存档层通过世界管理器读取区块与调试视图
- **THEN** 调用方仍取得当前状态，无法经查询结果修改世界组件
