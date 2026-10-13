# Design

## Context

`scripts/view/` 目前包含地面、玩家、怪物、雷达、地图、调试和聊天的 Godot 节点脚本。场景通过 `res://scripts/view/...` 路径引用它们。世界只读对象是持续读取当前组件的 `WorldChunksView`、`WorldDebugView`，现由 `chunks()`、`debug()` 返回。

## Goals / Non-Goals

**Goals:** 让目录名表达 UI 职责，让世界读取方法名表达返回视图；保持资源 UID、只读行为和场景可运行。

**Non-Goals:** 不重命名只读类型、不改变世界数据结构或视图订阅方式，也不拆分 UI 脚本目录。

## Decisions

- 将整个 `scripts/view/` 迁至 `scripts/ui/`，保留脚本文件名和 `.uid` 内容；同步 `.tscn` 中的资源路径。相比把地面和面板脚本拆到多个目录，这与当前已确认的统一 UI 命名一致。
- `WorldManager` 提供 `chunks_view()`、`debug_view()`，移除旧方法并更新调用点。方法采用现有 Godot-facing `snake_case` 风格；C# 返回类型保持原名。
- 当前规格的目录与只读入口要求同步更新；历史已归档 change 保留原始内容。

## Risks / Trade-offs

- 场景资源路径遗漏会在 Godot 导入或启动时失败 → 搜索全部旧路径并运行项目检查。
- 公开方法改名会影响仓库外部调用方 → 在变更和提交中明确标记为接口变更。
