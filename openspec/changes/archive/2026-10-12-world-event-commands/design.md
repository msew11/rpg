# Design

## Context

`main` 已提供按注解注册的 ECS 同步路由，`develop` 的 `WorldState` 原先直接实现多种交互命令。现有 GDScript 视图依靠世界信号刷新。

## Goals / Non-Goals

**Goals:** 世界交互只有一个投递入口；既有输入、存档和视图行为保持；类型命名表达协调职责。

**Non-Goals:** 改造会话持久化、改变游戏规则或引入异步事件队列。

## Decisions

- 将入口更名为 `WorldManager`。`WorldState` 暗示纯数据，但该类实际创建实体、调度系统、管理区块生命周期和信号。
- 事件对象继承 `WorldCommandEvent`，处理系统标记数据是否有效变化。管理器只根据变化类别发信号，不识别具体事件类型。
- 怪物创建系统按区块实体的 `ChunkIdComponent` 找到父实体，避免系统反向依赖管理器私有索引。

## Risks / Trade-offs

- 同步反射路由有调用开销 → 核心注册时缓存处理方法；当前输入规模适合此实现。
- 管理器仍持有区块持久化和视图通知逻辑 → 本 change 只移走交互规则，其他职责保持既有边界。
