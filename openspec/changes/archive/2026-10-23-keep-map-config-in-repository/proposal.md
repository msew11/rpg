# Proposal

## Why

地图管理器和小地图控件目前持有 `MapConfig` 引用，配置仓库未成为唯一持有者。统一通过仓库读取配置，明确数据所有权。

## What Changes

- 地图管理器和小地图控件持有 `RepoGameConfig`，需要数据时读取其 `MapConfig`。
- 主控制器、HUD 与测试改为传入仓库，而不是单独传递地图配置对象。
- 保持地图数值、加载格式与界面行为不变。

## Capabilities

纯内部结构调整，不改变现有规范行为，因此跳过 delta spec。

## Impact

涉及 `src/World/`、`src/UI/`、相关 C# 测试和当前架构文档。
