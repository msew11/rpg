# Proposal

## Why

区块拥有的运行时实体需要由父实体统一清理；组件也需要通用的双向字符串落地入口，供上层会话存档使用。当前核心只有平坦实体和直接销毁。

## What Changes

- ECS 支持实体父子关系、子实体查询和级联销毁。
- 组件基类提供可选的落地字符串与从字符串恢复方法，核心不处理文件。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `ecs-runtime`: 增加实体树和组件落地契约。

## Impact

仅修改 `scripts/core/`、`tests/core/`、相关核心 spec 与 `docs/ecs.md`；世界与存档层在 `develop` 使用新接口。
