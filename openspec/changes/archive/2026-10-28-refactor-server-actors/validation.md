# 验证记录

2026-10-28，在 develop 完成 Actor 重构。

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 最终完整运行通过：Godot 4.7.2 .NET 构建、导入、启动、270 项普通 xUnit、Godot xUnit 及全部场景验收。
- 新增 9 项回归覆盖：初始化屏障、Ask 回调与 Tell 顺序、Stop 排空、单邮箱不重入、分片独立推进、初始化/业务错误隔离、按 ID 唯一角色与共享世界、损坏角色重试和删除、新建角色在地图失败后的保留、只读地图快照及关闭、删除后的共同转移落地。部分行为合并在同一测试内。
- 原有移动、拾取取消、装备版本、摘要更新、存档事务与失败重试测试继续通过；仓库延迟缓存测试改为验证真实 Actor 运行时。
- `openspec validate refactor-server-actors --strict` 通过；同步后 `openspec validate --specs` 为 30 项通过，未修改能力保留原有 Purpose/长需求提示。
- 完整检查中曾出现一次光圈动画的计时断言失败；单场景复查及最后一轮串行完整运行均通过，未改动光圈实现或放宽断言。

当前 Actor 使用独立异步 Task 与串行邮箱，不绑定专用操作系统线程。本地 MockTransport 保留同步调用契约，仍等待 Actor 完成加载、处理或写入后返回。真实异步网络、多连接与跨地图迁移未纳入本次范围。
