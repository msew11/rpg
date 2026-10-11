## Why

现有测试的名称和目的不够直观，开发约定也逐项堆在同一层级。统一命名、标明需求来源并整理约定，可让后续功能增加时仍能快速判断测试保护的行为。

## What Changes

- `tests/` 下的 GDScript 测试及其专用替身统一使用 `test_` 文件名前缀，并更新所有引用。
- 在测试脚本的中文注释中写明对应 OpenSpec spec 的需求或场景；测试替身说明服务的契约。
- 用 Markdown 标题将 `AGENTS.md` 按分支、开发、验证和交付分类，并要求所有新 GDScript 类显式声明类名。
- 为现有未命名的测试脚本类补充 `class_name`，确保 Rider 可导航。

## Capabilities

### New Capabilities

无。本次只修改开发约定与测试组织方式。

### Modified Capabilities

无。现有数据访问和 ECS 行为要求不变。

## Impact

涉及 `AGENTS.md`、`tests/`、`tools/check.ps1` 和测试替身的文档路径。运行时公开 API 和游戏行为不变。
