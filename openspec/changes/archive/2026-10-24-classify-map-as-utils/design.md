## Context

当前 MapSystem 是静态类，提供 CreateMap、Clamp、TileAt、VisibleTiles，不持有运行状态，也不参与 IWorldSystem 调度。既有 Utils 已包含静态坐标投影工具。

## Goals / Non-Goals

**Goals:** 保留纯数据与行为分离，将地图纯计算按职责归入 Utils。

**Non-Goals:** 不引入系统实例、系统状态、空 Update 或新服务；不改变地图快照、裁剪和分块行为。

## Decisions

- 使用 internal static class MapUtils，移至 src/World/Utils，沿用全部方法和参数。
- WorldService 与 WorldManager 继续显式传入 DataMapComponent。工具不持有组件、服务或全局状态。
- ECS System 承担实体行为与系统调度；不需要系统身份的纯计算使用 Utils。将纯计算包装成实例不会增加实际职责，因此保留静态形式。
- 迁移相关验收和冒烟测试名称与引用，复用既有行为测试，不增加只检查类名或目录的测试。

## Risks / Trade-offs

- 旧命名可能残留 → 搜索 src、tests、docs 和主 specs，归档历史保留原记录。
- 迁移命名空间可能破坏构建 → 执行完整检查、全部验收及地图相关冒烟测试。
