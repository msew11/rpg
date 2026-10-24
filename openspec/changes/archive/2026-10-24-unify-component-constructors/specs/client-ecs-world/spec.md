## ADDED Requirements

### Requirement: Component 构造写法统一

`src/World/Components/` 中的组件 SHALL 使用普通 `internal sealed class`，SHALL 在类内部显式声明带简明中文注释的 `public` 块体构造方法，包含无参组件。组件 SHALL 将字段和属性的初始化赋值集中在构造方法中，不使用主构造函数、位置式 record 或隐式无参构造。

#### Scenario: 有参组件

- **WHEN** 定义需要外部初始数据的地图、角色身份或位置组件
- **THEN** 类名后不带构造参数列表，在类内部声明构造方法并为成员赋值，保持既有调用参数和读写边界

#### Scenario: 无参组件

- **WHEN** 创建动作或移动目标组件
- **THEN** 显式无参构造方法将动作设为 Stand、朝向设为 (1, 1)、动作版本设为 0，或将移动目标设为空，保持原有初始行为
