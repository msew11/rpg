# Proposal

## Why

三个 Context 同时展开持有业务数据、聚合快照和保存基线，重复了数据职责。将其收敛为 Context → 模块 Cache → DataCache → 数据，使状态修改、变化比较和数据管理归所属缓存。

## What Changes

- LoginContext、RoleContext、WorldContext 仅持有所属 Cache 引用及继承的借用服务入口。
- LoginCache、RoleCache、WorldCache 作为可变工作区，组织按数据类型划分的 DataCache。
- 共用抽象处理值、不可变数组、保存基线和提交；角色表差异、摘要内容比较和地图变化块归对应缓存。
- 仓库继续接收不可变导出数据，不修改协议、存档格式、邮箱及 IO 边界。

## Capabilities

### New Capabilities

### Modified Capabilities

- `server-v2-storage`: 明确 Context、Cache、DataCache 的状态归属及基线管理。

## Impact

V2 三个模块、数据缓存、Handler、相关测试和架构文档。并行的账号迁移继续完成，默认旧 Server 不变。
