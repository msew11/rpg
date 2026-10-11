## Context

`develop` 的 6 个功能测试仍采用 `*_test.gd` 文件名，检查脚本引用旧路径；9 个非核心脚本缺少顶层 `class_name`，一些非核心常量和类属性缺少中文注释。当前 Git 工作区干净，共享目录与 `main` 一致。

## Goals / Non-Goals

**Goals:** 对齐已有代理规约，并保持现有测试及游戏行为可运行。

**Non-Goals:** 不修改 `main` 归属文件、不改变玩法、场景结构或测试断言。

## Decisions

- 功能测试保留原有描述性词序，统一加 `test_` 前缀；对应 `.gd.uid` 随脚本更名并保留 UID 内容，检查脚本同步更换路径。
- 给每个无名脚本添加唯一的 PascalCase `class_name`。`TestChunkCatalog` 已是产品脚本类名，目录测试采用 `TestChunkCatalogScript`，避免全局名称冲突。
- 在每个功能测试文件头添加对应主规格路径和具体验收内容。仅补充非核心脚本的声明注释，不改动 `main` 维护的核心代码和测试。

## Risks / Trade-offs

- Godot 脚本 UID 或全局类名缓存可能在重命名后暂时失效 → 运行完整 `tools/check.ps1`，其中包含编辑器导入、主场景和全部测试。
- 大量注释变更容易掩盖行为修改 → 只改声明邻近注释与名称，检查差异并确认无逻辑变化。
