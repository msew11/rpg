## Why

当前 `develop` 的功能测试文件名、部分 GDScript 类名与成员注释不符合 `AGENTS.md`，测试说明也缺少需求或场景来源。这使 Rider 查找、检查脚本和后续验收难以遵循同一约定。

## What Changes

- 将功能测试脚本统一改为 `test_` 前缀，并更新检查入口。
- 为缺少名称的脚本显式声明顶层类名，为现有非核心脚本的常量和类属性补充简明中文注释。
- 在功能测试文件头标明所验证的 `openspec/specs/` 需求或场景。
- 保持运行行为、共享分支文件和核心目录内容不变。

## Capabilities

### New Capabilities

- `development-conventions`: 规定功能测试的命名和需求追踪，以及非核心 GDScript 的类名与成员注释。

### Modified Capabilities

无。

## Impact

影响 `scripts/` 的非核心 GDScript、`tests/core/` 以外的功能测试、`tools/check.ps1` 和 OpenSpec 文档；不改变游戏运行逻辑。
