# Design

## Context

`DataGameConfig` 当前仅包装一个 `DataMapConfig`；`RepoGameConfig` 按 `kind` 扫描并返回包装对象。地图 JSON 使用演示文件名。

## Goals / Non-Goals

**Goals:** 仓库持有成功加载的地图配置；文件名、类型与命名空间对应；保留现有验证和错误诊断。

**Non-Goals:** 不改变地图数值、渲染、Schema 字段或增加新的配置类型。

## Decisions

- `MapConfig` 和 `TileOverride` 放在 `Rpg.Config.Data` 的 `MapConfig.cs`；`RepoGameConfig` 留在 `Rpg.Config`。
- `RepoGameConfig.Load` 与 `LoadGodot` 加载到仓库自身。公开只读 `MapConfig` 属性，未成功加载时抛出明确异常；先在局部变量完成全部校验，再一次性赋值，避免半成品状态。
- 递归扫描继续处理所有 JSON。地图文件必须名为 `map.json` 且 `kind` 为 `map`；其他文件仍作为未知配置报错。两个同名地图文件触发重复错误。
- 主控制器和 HUD 共享同一个仓库实例，地图管理和地图控件继续直接接收 `MapConfig`。

## Risks / Trade-offs

- 配置文件改名会让旧文件名不再可加载；这是本次明确要求，错误会包含文件路径。
- `RepoGameConfig` 变成有状态对象；测试验证失败重载不会替换已加载的配置。
