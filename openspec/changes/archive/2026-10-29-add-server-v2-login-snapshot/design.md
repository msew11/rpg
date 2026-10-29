# Design

## Context

动机见 proposal.md。Login 只有摘要一张表，现有摘要仓库已负责账号文件 IO；Role 使用聚合快照和 Context 的 Snapshot/Commit 接口。

## Goals / Non-Goals

统一模块内存快照入口，让未来 Login 增加表时有明确的聚合位置。本次只封装已有摘要，不增加业务、仓库或存档格式。

## Decisions

- DataLoginSnapshot 是不可变 record，持有 DataAccountRoleSummaries。账号身份继续归 LoginContext.Username 所有。
- LoginContext 构造和保存基线使用 DataLoginSnapshot，公开 Snapshot()/Commit(...)。Commit 在入口拒绝空快照，以固定账号键接纳摘要，并沿用逐字段和嵌套数组内容比较。恢复基线内容会取消 SummariesDirty。
- LoginActor 在 INIT 将仓库摘要包装成快照；Flush 从快照取摘要交给现有仓库，成功后接受基线。保留 IAccountRoleSummaryRepo，避免只有一张表时引入重复仓库抽象。
- 移除 ReplaceSummaries，统一更新全部调用。仅一张表仍使用 SummariesDirty，不预先增加多表标志。

## Risks / Trade-offs

- [源码接口变化] → 全仓库检索并更新调用；验证配置、加载、写回和网络验收。
- [浅层 record 相等不能替代摘要内容相等] → 保留现有深比较，不用快照 record 的默认相等结果判定待写。

## Migration Plan

无需文件迁移，JSON 仍为摘要数组。源码调用改用 Commit(snapshot)；回滚可直接还原本次提交。
