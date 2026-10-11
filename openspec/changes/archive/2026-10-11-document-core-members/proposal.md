## Why

现有核心注释规范只明确类和方法，`EcsWorld` 的类属性以及核心测试中的内部类和属性仍缺少职责说明。用户要求把内部类、常量和类属性纳入注释范围。

## What Changes

- 扩展中文注释约定，覆盖内部类、常量、类属性和方法，明确不要求逐一注释方法内局部变量。
- 补充核心脚本及核心测试中现有声明的职责注释。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `core-code-documentation`：扩展注释范围至内部类、常量及类属性，并覆盖 `tests/core/`。

## Impact

涉及 `AGENTS.md`、`scripts/core/`、`tests/core/` 和核心代码注释规范；不改变运行行为。
