# Design

## Context

参见 proposal.md。角色快照直接持有不可变表数组；登录快照目前包装含 Username 和 Roles 的仓库模型。

## Goals / Non-Goals

登录内存快照只承载摘要集合，账号身份由分片维护。本次不修改现有仓库接口、不新增业务校验。

## Decisions

- DataLoginSnapshot(Summaries) 直接接收 ImmutableArray<DataRoleSummary>，Context 同样持有该数组；省去内存快照中的仓库包装。
- Context 在构造和 Commit 时将 default 数组归一为空数组，避免默认值进入比较；沿用嵌套数组内容比较与保存基线逻辑。
- Actor 加载后取仓库结果的 Roles，保存前用固定账号键和快照数组构造 DataAccountRoleSummaries。保留已有仓库契约，转换只位于 IO 边界。

## Risks / Trade-offs

- [数组不能直接使用 record 默认相等判定内容] → 保留 SummaryComparer 与 SequenceEqual。
- [调用类型变化] → 全仓库检索、编译及持久化验收覆盖。
