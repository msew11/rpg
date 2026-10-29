# 验证记录

- 三个 Context 仅保留模块 Cache 引用及继承的借用服务；没有数据、保存基线、Snapshot 或 Commit 转发。
- 新 DataCache 契约测试 3 项通过：直接修改三表子缓存即时影响聚合待写状态、导出隔离和聚合拒绝无效结构；摘要管理失败无副作用、墓碑与编号；地图跨块移动、跨块顺序语义、恢复及默认数组。
- 既有存储、Actor 保存阻塞/失败重试、关闭补写和跨模块查询测试已适配 Cache 并通过；DataLoginSnapshot、DataRoleSnapshot、DataWorldSnapshot 仅作为不可变传输值保留。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 完整通过：.NET 339 项、Godot xUnit 292 项、全部新旧真实节点场景验收通过。
- 两个 change 及涉及的 V2 主规格严格校验通过，全库普通规格校验 35 项通过；其他既有规格的严格模式文档警告见账号迁移的验证记录。
- 同步更新架构、V2 文档、路线图及账号迁移设计，协议和文件仓库接口未因缓存重构改变，邮箱与连接框架未修改。
