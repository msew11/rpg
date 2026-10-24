## Context

WorldService 持有 MapManager，InitializeWorldAction 将其传入 DataMapComponent。MapManager 同时持有 ConfigService、稀疏索引和查询方法。MovementSystem 是逐帧角色系统，地图目前仅提供按需计算。

## Goals / Non-Goals

**Goals:** 组件只保存数据，地图行为收归 Systems；保持服务入口和现有逻辑坐标、裁剪及可见格算法。

**Non-Goals:** 不新增地图玩法、寻路、多地图切换、网络消息或逐帧地图任务。

## Decisions

1. DataMapComponent 保存 Width、Height、TileSize、DefaultTile 和 FrozenDictionary 稀疏索引。构造方法只赋值，无业务方法和服务依赖。MapSystem.CreateMap 将 MapConfig 转换为独立只读快照，避免原始配置集合变化污染世界。
2. MapSystem 是 Systems 下的无状态静态系统，所有查询显式接收地图组件。地图没有逐帧行为，不实现只适用于逐帧角色处理的 IWorldSystem；不增加空 Update 或新服务。单纯把 MapManager 改名仍会留下组件持有行为对象的问题，因此同时完成职责拆分。
3. WorldService 启动时构造只读地图数据，用于选角前后的既有查询入口。初始化 Action 携带该数据；WorldManager 建立世界实体，并从其组件调用 MapSystem 处理移动和修正裁剪。共享只读数据不会引入外部 ECS 写入。
4. 保留稀疏 O(1) 查询，不复制整张百万格地图；表现层继续通过 WorldService 查询，保持分块机制。

## Risks / Trade-offs

- 静态地图快照与配置持有少量重复数据 → 仅复制尺寸、字符串与稀疏覆盖索引，配置仍归 ConfigService；没有热更新需求。
- 系统不统一实现逐帧接口 → 明确按需查询与逐帧更新的不同调度方式，不扩大现有接口。
- 输入与 ECS 裁剪可能漂移 → 两者调用同一个 MapSystem.Clamp，并以边界移动及权威修正测试验证。

## Migration Plan

整体替换内部类型和测试调用，执行完整项目检查与验收测试，更新 specs 和长期文档后归档、本地提交；回滚该提交即可恢复原结构。

## Open Questions

无新增游戏设计决策。

## Validation

- C# 验收测试 76/76 通过，新增地图数据隔离、边缘可见格及移动/权威修正裁剪场景。
- tools/check.ps1 通过；客户端流程（含越界输入）、地图分块和角色动画冒烟测试通过。客户端流程中预期的启动失败日志用于验证清理恢复。
- 本 change 严格校验及两个受影响的主 spec 严格校验通过。
- 全部主 specs 严格校验为 10/12；main-branch-scope、windows-export-check 原有 Purpose 不足 50 字符，被严格模式视为失败，两文件本次未修改。
