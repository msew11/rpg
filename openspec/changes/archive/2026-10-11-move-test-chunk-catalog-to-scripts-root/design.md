## Context

`LocalSession` 在启动时实例化 `TestChunkCatalog`，并将其交给 `WorldState` 初始化固定测试区块。调用方通过全局类名引用，未使用旧脚本的显式资源路径。

## Goals / Non-Goals

**Goals:** 让测试内容读取器位于 `scripts/` 根目录，保留既有类名、UID 和行为。

**Non-Goals:** 调整测试地图格式、初始化时机或 ECS 核心。

## Decisions

- 连同 `.gd.uid` 一起移动脚本，使 Godot 资源身份稳定；无需改动通过 `class_name` 引用它的代码。
- 更新目录规格与架构说明；运行完整项目检查，验证导入、测试区块和主场景仍可用。

## Risks / Trade-offs

- [移动脚本导致资源解析失败] → 保留 UID，运行 Godot 导入和完整检查。
