# Proposal

## Why
当前每次移动和背包变动均重写角色全部 JSON；背包增大后，无关领域的数据也重复写入。按角色 ID 与领域拆分可降低每次提交的数据量。

## What Changes
- 新存档使用 `<id>.role.json` 与 `<id>.bag.json`，每次只提交变化领域。
- 保持协议与内存聚合快照；角色移动和删除只写 role，GM 和物品移动只写 bag。
- 兼容旧 `<id>.json`，首次保存时迁移，保留原文件；新格式优先，不以旧备份掩盖损坏。
- 创建、迁移和同时变更领域时提供持久化回滚记录；单文件提交保持原子替换。

## Capabilities
### New Capabilities
无。
### Modified Capabilities
- `mock-server`: 按领域持久化、兼容和恢复。
- `role-bag`: 背包独立原子保存。

## Impact
RepoServerRole、服务器领域持久化数据与 MockServer 保存调用，相关持久化、日志和流程验收；同步架构与路线图。不改变客户端协议。
