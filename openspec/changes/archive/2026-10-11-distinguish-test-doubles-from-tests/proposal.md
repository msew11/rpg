## Why

`test_` 应标识可执行测试。数据源替身只为测试提供依赖，当前同样使用 `test_`，容易让人误认为它也是独立测试入口。

## What Changes

- 可执行测试脚本保留 `test_` 前缀，测试替身改用 `double_` 前缀。
- 重命名现有内存数据源替身、显式类名及其引用，并保留 Godot UID。
- 更新 `AGENTS.md` 的命名规则和数据层文档路径。

## Capabilities

### New Capabilities

无。此变更只调整开发约定和文件命名。

### Modified Capabilities

无。数据访问契约及测试行为不变。

## Impact

涉及 `AGENTS.md`、`docs/data-layer.md`、`tests/doubles/` 和数据源契约测试的预加载路径；运行时 API 不变。
