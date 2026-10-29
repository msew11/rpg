# Proposal

## Why

Login 快照已直接持有摘要数组，账号身份由分片维护。DataAccountRoleSummaries 仅为仓库增加一次包装，用户要求移除。

## What Changes

- **BREAKING**：移除 DataAccountRoleSummaries，摘要仓库 LoadAsync 返回不可变摘要数组，SaveAsync 单独接收 username 和数组。
- Actor 直接连接快照数组与仓库，不再包装和解包；保持账号路径安全、缺失空表、default 数组归一和 JSON 数组格式。
- 更新测试及当前文档，按会话要求合并进上次提交。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。结构重构使用 skip_specs，保留既有 server-v2-storage 与 server-v2-account 行为契约。

## Impact

Login 数据模型、摘要仓库接口、Actor、测试替身与验收调用。无需存档迁移。
