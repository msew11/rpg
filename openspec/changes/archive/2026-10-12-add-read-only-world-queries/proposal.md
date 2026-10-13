# Proposal

## Why

`WorldManager.chunks()` 和 `debug()` 直接返回可变 ECS 组件。展示层与存档层目前只需要读，但调用方可改动尺寸、加载范围和调试开关，绕过世界规则与变化通知。

## What Changes

- **BREAKING** 世界管理器改为返回只读区块与调试视图，不再公开这两种可变组件。
- 加载区块列表和地块数组通过查询返回独立副本；展示和存档所需的读能力保持可用。
- 更新调用方、验收测试及边界文档。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-script-boundaries`: 世界管理器的区块及调试查询不得让调用方修改权威世界组件。

## Impact

影响 `WorldManager` 的查询返回类型、世界视图脚本和测试；不改变交互命令、区块存档格式或玩法。
