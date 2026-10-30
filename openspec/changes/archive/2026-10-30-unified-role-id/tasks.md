# Tasks

## 1. 统一取号与持久化

- [x] 1.1 实现共享递增生成器及编号段持久化仓库，增加并发、重启、故障、耗尽测试并验证通过。
- [x] 1.2 接入 LoginActor 取号与缓存提交，删除账号区间算法；更新多账号和角色管理回归及服务端、架构、路线图说明，验证 Handler 不执行 IO。

## 2. 集成检查与清理

- [x] 2.1 确认游戏进程关闭，按授权清除本机角色数据与角色设置，验证清除范围和保留文件。
- [x] 2.2 运行 tools/check.ps1 -Tests，核对文档、specs 和代码一致，记录检查结果。

## 检查结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：构建零警告和错误，354 项普通 .NET 测试、338 项 Godot xUnit，以及全部真实界面和双进程 TCP 验收通过。
- 新增 9 项生成器测试覆盖 2000 次并发、跨预留段、无摘要重启、损坏/非法文件、预留写盘失败重试、独占锁及 int64 耗尽；新增 2 项账号集成测试覆盖三个账号并发共享序列和预留失败不提交摘要，更新耗尽及重启回归。
- 本机无游戏进程运行时清除 8 个旧角色存档文件和 2 个角色设置文件，系统设置保留，清理后无角色文件。
- `openspec validate unified-role-id --strict` 通过；`openspec validate --specs` 全部 38 项通过，其他既有 specs 的 Purpose/长度警告未改动。

## Workflow follow-up

- 同步主 specs，归档 change，在 develop 创建中文约定式本地提交。
