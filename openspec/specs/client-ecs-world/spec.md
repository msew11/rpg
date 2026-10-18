# client-ecs-world Specification

## Purpose
TBD - created by archiving change data-driven-rpg-client. Update Purpose after archive.
## Requirements
### Requirement: 世界仅通过 Action 改变
`WorldManager` SHALL 是 ECS 的唯一写入入口；输入事件和外部状态更新 SHALL 转为 Action，由 Godot 固定帧处理。世界实体 SHALL 先于角色实体创建。

#### Scenario: 初始化顺序
- **WHEN** 选定角色并进入世界
- **THEN** ECS 先加载地图世界实体，再初始化角色实体

#### Scenario: 外部位置修正
- **WHEN** 会话缓存收到模拟源位置修正
- **THEN** 修正经同步 Action 进入 ECS，而非直接修改组件

### Requirement: 点击移动可改目标并校正
鼠标点击 SHALL 产生移动 Action；角色在边界内直线移动，后续点击 SHALL 更新目的地；延迟确认 SHALL 按序号校正位置并忽略过期确认。

#### Scenario: 连续点击
- **WHEN** 角色向第一目标移动时再次点击
- **THEN** 角色改向第二目标移动

#### Scenario: 权威修正
- **WHEN** 模拟源返回当前意图的确认位置
- **THEN** ECS 应用修正，并保持比该确认更新的未确认目标
