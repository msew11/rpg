## Why

ECS 的组件类型、组件实例和系统实例由调用方确定，公开 API 将这些入参标为可空会误导调用方，并增加无意义的空值分支。

## What Changes

- 将 ECS 公开方法中的组件与系统入参声明为非空类型。
- 保留“查找不到组件或仓库时返回空值”的返回值契约。
- 删除与非空入参相矛盾的空值分支，现有实体与系统行为不变。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `ecs-runtime`: 明确组件与系统 API 的非空入参契约。

## Impact

影响 `scripts/core/ecs/EcsWorld.cs` 与核心文档；现有验收测试继续验证有效输入、无效实体和缺失组件场景。
