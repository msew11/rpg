## Context

当前 ECS 核心按注册顺序调用 `EcsSystem.update(world, delta)`。`MovementSystem` 仅使用参数，`RadarSystem` 与 `WorldMapSystem` 却由 UI 创建，持有世界弱引用、玩家 ID 与缓存；`ChunkWorld` 在 ECS 外更新区块范围。项目当前只有一个本地玩家，测试世界固定为 4 × 4 个规范区块。

## Goals / Non-Goals

**Goals:** 统一 `scripts/world/` 中组件与无状态 System 的边界；顶层 `scripts/utils/` 仅存放无状态静态计算；具体 UI 留在 `view/`；将 `main.gd` 和 `local_session.gd` 提到 `scripts/` 根目录；保持现有可观察行为。

**Non-Goals:** 修改 `scripts/core/`、玩法、地图素材、跨运行存档或联网。

## Decisions

- `WorldState` 和类型组件归 `world/`；`world/systems/` 仅放通过 `EcsWorld.step()` 更新组件的系统。`ChunkLoadingSystem` 根据玩家位置和世界区块组件计算 3 × 3 加载范围；首次与跨区块均按同一规则更新。为避免 System 保存玩家 ID，玩家增加用于查询的标记组件。
- 测试目录仅在世界初始化时读取；4 × 4 个规范区块预先写入 `WorldChunksComponent`。这是固定小型测试内容，避免 System 持有文件读取器，仍只显示玩家附近 3 × 3 的行走区块。地块切换直接修改组件中的既有数据，回环复用规范 ID。
- `UtilChunkCoordinates`、`UtilIsometricProjection`、`UtilRadarProjection`、`UtilWorldMapProjection` 均使用静态函数。雷达和地图调用时传入 `WorldState`、玩家 ID、视口尺寸或拖动偏移，不持有世界引用或结果缓存。UI 管理可见性、拖动状态和绘制。
- `.gd.uid` 随脚本移动并保持原内容；以类名引用的调用点同步重命名。场景入口脚本的资源路径同步更新。

## Risks / Trade-offs

- [初始化从按需变为全部读取] → 当前仅 16 个规范区块，检查地块编辑、远离和回环行为；后续大地图另行设计流式数据源。
- [系统注册顺序影响加载范围] → 注册移动系统后再注册区块加载系统，并验证跨边界同一物理步更新。
- [UI 改用静态工具后漏传参数] → 运行雷达、地图与拖动测试，并执行项目检查。

## Migration Plan

先建立规范与任务，再迁移文件及引用，随后调整区块加载和 UI 工具调用；更新架构文档、运行检查后归档 change 并本地提交。回滚可撤销单个约定式提交。

## Open Questions

无。更大地图的流式载入策略不在本次范围内。
