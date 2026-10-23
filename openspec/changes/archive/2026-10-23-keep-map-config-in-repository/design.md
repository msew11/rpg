# Design

## Context

`RepoGameConfig` 已持有加载成功的地图配置，但 `MapManager.Config` 和 `MiniMapControl.Map` 仍保存同一 `MapConfig` 引用。

## Goals / Non-Goals

**Goals:** 仓库是地图配置唯一持有者；下游持有仓库并在操作时读取地图配置。

**Non-Goals:** 不改变地图算法、JSON、Schema 或增加运行时热重载能力。

## Decisions

- `MapManager` 接收并保存 `RepoGameConfig`，继续按加载时的覆盖格建立索引；仅从仓库读取尺寸和默认地块。
- `MiniMapControl` 接收仓库，在绘制时读取其 `MapConfig`；HUD 和主控制器只传仓库。
- 测试从项目配置加载仓库，避免用独立 `MapConfig` 实例构造地图管理器。

## Risks / Trade-offs

- 地图管理器的覆盖格索引仍基于构造时已加载的地图；运行时重新加载仓库后需重建世界，热重载不在本次范围。
