## Why

背包和聊天目前只有占位，无法验证角色持有物品的存档与同步。通过聊天 GM 发放道具，建立可验证的背包展示闭环。

## What Changes

- 单例基础配置提供 8 列、5 行，角色独立保存增加行数。
- 道具实例随角色原子持久化，选角同步完整背包，变更推送完整快照。
- 聊天输入 `/item <配置ID> [数量]` 发放当前角色道具，容量不足全部失败。
- 格子展示图标、数量和名称提示，不实现聊天及背包操作或装备属性。

## Capabilities

### New Capabilities
- `role-bag`: 背包容量、实例持久化、协议缓存和图标展示。
- `gm-item`: 聊天命令、服务端发放和执行反馈。

### Modified Capabilities
- `client-item-config`: 道具配置增加可选图标素材标识，正式七件装备显式绑定。

- `game-services`: 网络构造接收 Config 并复制服务端规则。
- `client-config-cache`: 注册基础单例缓存。
- `client-network`: 注册背包与 GM 协议。

## Impact

配置、protobuf、MockServer、角色存档与缓存、HUD、测试和长期文档。NetService 增加 Config 构造依赖以复制服务端配置，不增加第三方依赖。
