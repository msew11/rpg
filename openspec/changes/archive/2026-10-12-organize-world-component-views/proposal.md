# Proposal

## Why

世界区块与调试组件的只读视图仍位于 `scripts/world/` 根目录。将其集中放置能让目录直接反映组件视图职责。

## What Changes

- 将 `WorldChunksView.cs` 和 `WorldDebugView.cs` 及各自 UID 移至 `scripts/world/compviews/`。
- 保留类型名称、只读接口和 Godot 注册身份，更新架构目录说明。

## Capabilities

### Modified Capabilities

- `world-script-boundaries`：明确世界组件视图的目录归属。

## Impact

影响两个 C# 文件及 UID 的路径、架构文档和相关检查；不改变世界状态、存档格式或 UI 行为。
