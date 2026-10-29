# Proposal

## Why

DataLoginSnapshot 中再包装 DataAccountRoleSummaries，令模块快照重复携带账号身份，访问集合也多一层。用户要求与 DataRoleSnapshot 一样直接使用 ImmutableArray。

## What Changes

- **BREAKING**：DataLoginSnapshot.Summaries 和 LoginContext.Summaries 改为 ImmutableArray<DataRoleSummary>。
- 账号身份固定在 Actor/Context，现有摘要仓库包装仅在 Actor 的加载和保存边界转换。
- 更新调用、测试和文档，完成后合并进上次本地提交。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。结构重构使用 skip_specs，保留既有 server-v2-storage 的内容比较、写回、身份及文件契约。

## Impact

V2 Login 数据、上下文、Actor 与调用测试；仓库接口与 JSON 格式保持原有契约。
