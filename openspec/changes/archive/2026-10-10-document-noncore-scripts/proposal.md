## Why

现有非核心 GDScript 类与方法缺少统一的职责说明，阅读时需要反复追踪调用。现行开发约定已要求简明中文注释，用户希望将它应用到现有脚本。

## What Changes

- 为 `scripts/core/` 之外的项目脚本和测试脚本中的类、方法补充简明中文注释。
- 保持代码执行逻辑、场景和资源不变。

## Capabilities

仅调整代码注释，不改变游戏行为，因此跳过规格增量。

## Impact

涉及 `scripts/` 中除 `core/` 外的 GDScript 和 `tests/` 下的 GDScript；`scripts/core/` 保持与 `main` 一致。
