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

### Requirement: Component 构造写法统一

`src/World/Components/` 中的组件 SHALL 使用普通 `internal sealed class`，SHALL 在类内部显式声明带简明中文注释的 `public` 块体构造方法，包含无参组件。组件 SHALL 将字段和属性的初始化赋值集中在构造方法中，不使用主构造函数、位置式 record 或隐式无参构造。

#### Scenario: 有参组件

- **WHEN** 定义需要外部初始数据的地图、角色身份或位置组件
- **THEN** 类名后不带构造参数列表，在类内部声明构造方法并为成员赋值，保持既有调用参数和读写边界

#### Scenario: 无参组件

- **WHEN** 创建动作或移动目标组件
- **THEN** 显式无参构造方法将动作设为 Stand、朝向设为 (1, 1)、动作版本设为 0，或将移动目标设为空，保持原有初始行为

### Requirement: 地图组件与行为分离

ECS Component SHALL 仅承载数据，不持有服务、管理器或业务方法。DataMapComponent SHALL 保存只读地图尺寸、瓦片大小、默认地块和稀疏地块索引；MapUtils SHALL 承担数据构建及地图计算。初始化世界的 Action SHALL 只携带地图数据，WorldManager SHALL 从世界实体的组件读取数据执行地图计算。

#### Scenario: 地图初始化数据隔离

- **WHEN** 从配置构造地图数据并通过 Action 初始化世界
- **THEN** 组件不包含 MapManager 或 ConfigService，后续原始覆盖格集合变化不改变已构造的地图数据

#### Scenario: 越界输入与权威修正

- **WHEN** 移动 Action 或被接纳的权威修正位置超出地图边界
- **THEN** WorldManager 使用 MapUtils 和世界地图组件将其裁剪至有效逻辑范围，输入发送前采用同一规则
