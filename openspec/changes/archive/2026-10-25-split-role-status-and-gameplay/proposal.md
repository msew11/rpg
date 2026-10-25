# Proposal

## Why

当前角色资料同时承载外观、生命魔力及仅玩家自己可见的经验和背包扩容。世界状态未来可能提供给其他玩家，因此私有玩法数据必须离开 RoleWorldStatePb，状态变化也需要独立传输。

## What Changes

- **BREAKING** 将生命和魔力移入 RoleWorldStatusPb，世界完整快照包含资料、状态和位置。
- **BREAKING** 将经验、升级经验和背包扩容移入独立 RoleGameplayPb；选角响应单独返回该私有数据。
- 提供 RoleStatusNotice 与 RoleGameplayNotice，并让缓存分别合并四类数据。

## Capabilities

### Modified Capabilities

- `mock-server`: 区分可见世界状态和当前玩家私有玩法数据。
- `client-session`: 分别缓存、校验和更新资料、状态、位置及私有玩法数据。

## Impact

修改 protobuf、生成消息、MockServer、RoleCache、HUD、协议验收及文档；存档格式不变。
