# Design

## Context

`ChunkLoadingSystem` 只计算行走区块加载列表；`WorldManager.step()` 随后比对前后列表，直接管理规范区块实体、缓存和 JSON。怪物查询也在管理器内遍历 ECS 子实体并投影回环位置。现有 `LocalSession`、`SaveSlots` 和 UI 使用管理器公开方法。

## Goals / Non-Goals

**Goals:** 将区块生命周期、实体树编解码、怪物可见位置查询分别迁出；保持公开接口、通知时机、存档 JSON、回环和显示结果。

**Non-Goals:** 不改变区块加载规则、ECS System、存档槽位格式或怪物生成规则。

## Decisions

- `WorldChunkLifecycle` 持有 ECS、缓存、世界实体 ID、区块组件和配置，响应 `step()` 后的加载列表变化；临时写入失败通过回调交给管理器发出既有错误信号。它维护规范 ID 到活跃区块实体的映射。
- `WorldEntityTreeCodec` 负责已有 JSON 结构的采集、校验、恢复。保留管理器的 `capture_chunk_entities()` 和静态 `can_restore_entities()` 作为兼容入口，内部转发到新类。
- `WorldMonsterQuery` 只读取活跃行走区块、规范实体映射和 ECS 组件，返回原有回环位置。管理器公开方法继续转发查询结果，供 UI 使用。
- `WorldManager` 仍在 ECS `Step` 前后采集可见状态并发信号；实体树快照改由生命周期类调用编解码器取得。

## Risks / Trade-offs

- 生命周期对象持有配置和缓存，但不自行推进 ECS；保留 `step()` 中明确的调用顺序以避免通知和卸载时机变化。
- 保留管理器公开兼容入口会留少量转发方法，避免现有 GDScript 与测试同时迁移造成行为风险。
