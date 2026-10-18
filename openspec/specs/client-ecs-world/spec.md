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

### Requirement: 等轴世界表现

世界表现 SHALL 把地图逻辑坐标绘制成 2:1 的 2D 等轴菱形地块；角色与摄像机 SHALL 使用同一投影。点击地图时输入层 SHALL 逆投影至逻辑坐标后提交移动 Action，不改变 ECS 的逻辑坐标。

#### Scenario: 等轴地图和角色

- **WHEN** 进入角色世界
- **THEN** 可见地块呈等轴菱形，角色位于其逻辑位置对应的投影位置，地图仅遍历视口附近的瓦片

#### Scenario: 等轴点击移动

- **WHEN** 玩家点击地图的一个投影位置
- **THEN** 移动目标对应逆投影后的逻辑位置，并仍受地图边界限制
