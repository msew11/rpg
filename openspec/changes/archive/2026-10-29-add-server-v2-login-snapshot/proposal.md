# Proposal

## Why

Login 当前直接用账号摘要作为状态和保存基线，缺少与 Role 一致的模块快照入口。新增登录聚合快照，统一 Context 的取快照和提交方式。

## What Changes

- 新增不可变 DataLoginSnapshot，当前包含 DataAccountRoleSummaries。
- LoginContext 使用 Snapshot()/Commit(DataLoginSnapshot)，保存基线改为登录快照；Actor 在加载与保存边界连接摘要仓库。
- **BREAKING**：用 Commit 替代 ReplaceSummaries，更新调用与验收。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。本次是内部结构重构，既有 server-v2-storage 的加载、统一修改、按内容标记、失败保留与写回契约不变；使用 skip_specs，不新增或修改行为规格。

## Impact

V2 Login Data、Context、Actor 与相关测试和文档。摘要仓库接口、JSON 格式、账号路径规则、非空依赖及默认登录占位保持不变。
