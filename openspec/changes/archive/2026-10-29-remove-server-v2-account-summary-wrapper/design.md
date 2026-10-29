# Design

## Context

动机见 proposal.md。Context 和快照已使用 ImmutableArray<DataRoleSummary>，只有仓库使用包含 Username/Roles 的旧包装。

## Goals / Non-Goals

使身份参数和表数据在仓库接口显式分离，消除冗余模型。不新增业务规则、不修改文件结构。

## Decisions

- IAccountRoleSummaryRepo.LoadAsync(username) 返回 ImmutableArray<DataRoleSummary>，SaveAsync(username, summaries) 保存整张表。仓库面向单张摘要表，不再引入聚合包装。
- RepoAccountRoleSummary 在文件操作前通过 UsernameKey.SummaryFileName 规范化和检查账号；default 数组按 [] 写入，缺失仍返回 []，解析和 IO 错误继续传播。
- LoginActor 使用固定 _username 保存 Context 快照中的数组，保持邮箱内串行和成功后更新基线的顺序。

## Risks / Trade-offs

- [删除模型令路径规范化责任失去原入口] → 在真实仓库入口保留 UsernameKey，验收非法账号在创建目录前被拒绝。
- [仓库契约变化] → 更新全部替身与调用，运行加载、文件格式、失败重试及关闭补写验收。
