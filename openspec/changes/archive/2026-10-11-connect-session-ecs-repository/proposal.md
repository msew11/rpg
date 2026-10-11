## Why

本地会话需要统一持有运行时数据仓库，使 ECS 世界数据有明确的会话归属，并为后续存档收集提供入口。

## What Changes

- 增加由 GDScript 直接创建的 `LocalDataRepoCache`。
- 增加承载 `DataEcsWorld` 的单例 `RepoWorldEcs`。
- `LocalSession` 创建 Cache，并将 `WorldState` 的 ECS 数据绑定到其中。

## Capabilities

### Added Capabilities
- `session-data-repository`: 本地会话独占缓存及 ECS 数据仓库。

## Impact

修改 `develop` 世界与会话代码、功能测试、数据层与架构文档；不实现存档格式、组件字段选择或增量保存。
