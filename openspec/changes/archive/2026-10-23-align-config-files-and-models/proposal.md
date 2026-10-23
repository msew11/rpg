# Proposal

## Why

配置文件、数据类型和加载后的组合对象目前名称不对应。直接让配置仓库持有各类已校验配置，可简化配置链路并明确文件契约。

## What Changes

- 地图配置文件改为 `config/maps/map.json`，地图标识改为 `map`。
- 地图数据类型改为 `Rpg.Config.Data.MapConfig`，瓦片覆盖类型改为 `TileOverride`；移除 `DataGameConfig`。
- `RepoGameConfig` 加载成功后持有 `MapConfig`，要求地图 JSON 文件名为 `map.json`。
- 更新使用方、测试和当前架构文档。

## Capabilities

### Modified Capabilities

- `client-config-map`: 地图配置文件名与类型对应，错误文件名需明确拒绝。

## Impact

涉及 `config/`、`src/Config/`、地图和 UI 调用方、测试与 `docs/architecture.md`；不新增依赖。
