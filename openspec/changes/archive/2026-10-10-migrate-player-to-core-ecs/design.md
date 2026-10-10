# Design

## Context

共享 `EcsWorld` 提供实体 ID、按脚本类型存取组件、查询及有序系统调度，但不提供字段变化信号。现有视图与区块使用 `WorldState` 的具名键及 `value_changed`。

## Decisions

- `WorldState` 持有一个 `EcsWorld`，在初始化时注册移动系统；玩家创建时建立四个组件及其原有具名键到组件的映射。组件值是玩家状态唯一存储位置。
- `MovementSystem` 继承 `EcsSystem`，查询具备四个移动组件的实体，在固定步长中修改位置与朝向。`WorldState.step` 调用 ECS，并为位置和朝向的有效变化发出原有数据源通知。
- 非实体的区块、调试键继续由 `WorldState` 的内存字典保存，维持复制可变值和通知语义。
- `LocalSession` 仍是输入及读取入口；其 `step` 转由 `WorldState.step` 执行。视图代码不持有 ECS 对象。

## Risks / Trade-offs

- ECS 组件可被外部直接修改，但直接修改不产生 `DataSource` 通知；原型的写入路径仍经 `WorldState`，移动更新由其 `step` 发布变化。后续若新增直接写组件的系统，应在适配边界加入相应通知。
- 每步对玩家位置与朝向做快照比较，适合当前少量实体原型；大规模实体时可改为显式变更收集。

## Verification

确认玩家 ID 与 ECS 实体相同，具名键读写与组件一致，移动后通知位置和朝向；运行现有移动、视图重建、区块和完整项目检查。
