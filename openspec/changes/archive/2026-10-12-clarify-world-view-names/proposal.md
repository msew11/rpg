# Proposal

## Why

`scripts/view/` 表示画面节点，而 `WorldChunksView`、`WorldDebugView` 表示世界状态的只读视图；目录和类型中的 `view` 指向不同职责。世界管理器的 `chunks()`、`debug()` 也未直接表明返回的是视图对象。

## What Changes

- **BREAKING** 将世界管理器的只读入口改名为 `chunks_view()`、`debug_view()`，保留原有只读类型与行为。
- 将 Godot 展示和交互脚本目录从 `scripts/view/` 改为 `scripts/ui/`，同步场景资源路径。
- 更新当前代码、测试、规格和文档中的名称与路径。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-script-boundaries`：明确 UI 脚本目录与世界只读视图入口。

## Impact

影响 `scripts/world/WorldManager.cs` 的公开方法名、GDScript/C# 调用点、`scenes/` 资源路径及文档。不会改变运行时规则、视图数据或存档格式。
