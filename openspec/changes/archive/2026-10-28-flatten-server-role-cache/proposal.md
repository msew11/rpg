# Proposal

## Why

角色三份实体缓存经由 RoleEntityTables 再挂到 ServerRoleCache，增加了不必要的容器层级。用户要求由 ServerRoleCache 直接持有这些缓存并删除 RoleEntityTables。

## What Changes

- ServerRoleCache 直接持有 Role、Item、Equipment，以及原有待写领域和会话。
- 迁移跨表校验、候选构造及内容比较，调整仓库、Handler、Actor 与测试调用。
- 保持请求拒绝的原子性、工作副本隔离、摘要懒加载、存档结构及失败重试行为。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。本次为内部结构重构，沿用 mock-server、server-actors 和 server-write-back 的既有要求，设置 skip_specs。

## Impact

影响 src/Server、服务端相关 C# 测试及架构和路线图说明。不修改协议、存档格式或玩法。
