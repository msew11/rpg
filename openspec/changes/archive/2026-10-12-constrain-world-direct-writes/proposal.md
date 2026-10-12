# Proposal

## Why

世界管理器仍公开玩家创建、位置与朝向设置、地块覆盖、区块初始化和卸载方法。运行期调用方可绕过事件规则，甚至让区块索引与加载范围失配。

## What Changes

- **BREAKING** 把零散直接写入方法从世界管理器的公开 API 收起；区块卸载只由管理器的跟随流程触发。
- 本地会话使用一次性的世界初始化入口建立玩家和测试区块；正式存档恢复通过同一入口传入已验证的初始状态。
- 测试通过 `tests/` 专用辅助类布置位置、朝向和地块样本，继续验证通知、区块跟随与存档行为。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `world-script-boundaries`: 区分会话初始化、存档恢复、测试布置与运行时交互，阻止生产调用方在运行期使用零散直接写入入口。

## Impact

影响 `WorldManager` 公开 API、`LocalSession` 启动流程、测试辅助类与相关测试；不改动存档格式或现有交互规则。
