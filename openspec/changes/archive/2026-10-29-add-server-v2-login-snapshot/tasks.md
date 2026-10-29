# Tasks

## 1. 登录聚合快照

- [x] 1.1 新增 DataLoginSnapshot，统一 Context 与 Actor 的快照加载、提交和保存入口，更新测试调用；以快照隔离、内容比较、基线恢复、拒绝空提交和已有持久化测试验证。
- [x] 1.2 更新 V2 文档、架构与路线图；核对文档中的 Snapshot/Commit 和现有摘要文件契约与代码一致。

## 2. 集成检查

- [x] 2.1 执行 tools/check.ps1 -Tests 与 OpenSpec 严格校验，记录完整结果及已有不稳定场景的复验结果。

## Workflow follow-up

- 归档完成的 change，保留本地中文 Conventional Commit，不推送。
