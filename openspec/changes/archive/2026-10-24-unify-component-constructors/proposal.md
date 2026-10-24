## Why

现有五个 ECS Component 混用位置式 record、主构造函数、普通构造方法和隐式无参构造，增加阅读成本。统一构造写法并记录规范，避免后续继续混用。

## What Changes

- Component 统一为普通 sealed class，在类内部显式声明带中文注释的块体构造方法。
- 初始化集中在构造方法内；无外部参数的组件也显式声明无参构造方法。
- 保留现有调用参数、只读与可写边界、默认状态及世界行为。
- 在架构文档记录规范，在路线图记录完成状态。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ecs-world`: 增加 Component 类型、构造方法和初始化位置的统一约定。

## Impact

影响 src/World/Components 下五个组件及开发文档；不新增依赖。地图组件不再提供未使用的 record 值相等、解构和 with 复制能力。通过现有世界与角色验收测试和项目检查验证运行行为。
