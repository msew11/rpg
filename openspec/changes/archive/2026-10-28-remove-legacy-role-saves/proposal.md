# Proposal

## Why

当前仍为开发期，用户允许清空旧 JSON，无需维护历史角色存档。旧聚合、role/bag 双文件和旧持握迁移增加了额外结构与分支，应统一收敛到现行实体四表。

## What Changes

- **BREAKING** 删除 DataServerBagFile、旧路径 API、旧格式读取和摘要回填；角色仅使用 role/item/equipment/role_summary 四表。
- 删除旧装备自动迁移与版本 1 转移记录恢复；选角严格检查当前装备规则。
- 移除服务端实体针对旧 JSON 的字段默认兼容，保留 DataServerBagRules 的现行业务职责。
- 清理已确认的本地 8 个旧角色 JSON；更新测试、当前规范和架构文档。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `mock-server`：仅支持当前角色实体表，摘要不得通过旧文件回填。
- `role-bag`：移除旧聚合存档空背包兼容要求。
- `role-equipment`：删除旧持握存档自动迁移要求。
- `role-management`：以当前完整摘要列出角色，缺少存档头型不再自动补齐。
- `server-write-back`：多文件回滚只处理现行实体表。
- `ground-item-drop`：共同转移不再迁移旧角色格式，事务记录仅支持当前版本。

## Impact

涉及 Server 数据、实体、角色仓库、转移事务、选角校验及对应测试。现有协议、地图 manifest/chunk 和客户端设置保持现行行为。已确认的旧角色 JSON 会删除，后续通过创建角色重新开始。
