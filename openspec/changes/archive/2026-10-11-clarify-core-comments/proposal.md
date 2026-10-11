## Why

`scripts/core/ecs/` 的类和方法缺少中文职责说明，部分现有注释仍是英文，不符合项目的代码注释约定。

## What Changes

- 为 ECS 核心类和方法补上简明中文注释，说明职责和调用行为。
- 核对数据仓库抽象类的既有注释，保持核心目录注释风格一致。

## Capabilities

### New Capabilities

- `core-code-documentation`：核心类和方法的注释约定。

### Modified Capabilities

无；运行时契约不变。

## Impact

仅涉及 `scripts/core/` 的注释，不改变 GDScript API 或执行逻辑。
