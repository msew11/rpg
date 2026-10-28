# Proposal

## Why

当前 RoleEntityCache 同时持有角色、物品和装备缓存，使角色单例承担了仓库聚合职责。三个表缓存应处于平级，由外层统一组合，符合单例与多例表各自独立的约定。

## What Changes

- RoleEntityCache 仅持有单个 RoleEntity。
- 新增 RepoEntityCache，平级持有 RoleEntityCache、ItemEntityCache、EquipmentEntityCache，承接业务候选投影与组合校验。
- RepoServerRole 使用 RepoEntityCache 读写和缓存完整实体组；更新现有回归与架构文档。

## Capabilities

### New Capabilities

无。此变更为内部职责重构，使用 skip_specs。

### Modified Capabilities

无。mock-server 与 server-write-back 的登录懒加载、分表存档、事务和版本行为保持既有契约。

## Impact

涉及 Server/EntityCache、RepoServerRole、RoleEntityStorageTests 及架构和路线图文档；没有协议或存档格式变更。
