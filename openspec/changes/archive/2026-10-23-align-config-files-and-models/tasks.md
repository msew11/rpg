# Tasks

## 1. 配置仓库与契约

- [x] 1.1 将地图 JSON 改为 `map.json`，数据类改为 `MapConfig` 与 `TileOverride`，并验证资源与 UID 路径一致。
- [x] 1.2 让 `RepoGameConfig` 持有成功加载的 `MapConfig` 并校验文件名，增加加载、错误文件名及失败重载测试并验证单测通过。

## 2. 调用方与文档

- [x] 2.1 更新 UI、地图和冒烟测试中的配置类型与访问路径，验证 Godot 冒烟场景通过。
- [x] 2.2 更新当前架构文档和路线图，验证描述与代码及规范一致。

## 3. 集成检查

- [x] 3.1 运行 `tools/check.ps1` 和 OpenSpec 严格校验，确认全部通过。

## Workflow follow-up

- 同步规范、归档 change，并提交到本地 `develop`。
