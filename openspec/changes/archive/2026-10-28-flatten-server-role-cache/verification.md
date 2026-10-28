# 验收记录

日期：2026-10-28。

- ServerRoleCache 直接持有 Role、Item、Equipment，不保留 Tables 属性；src、tests 和当前架构文档均无 RoleEntityTables 引用，旧类文件已删除。
- 新增 7 个测试用例，验证角色工作副本隔离会话及其可写集合、三种领域提交保留会话和已有待写状态，以及编号越界、重复背包格、重复装备槽的候选被拒绝且原缓存不变。
- 相关服务端测试：46 项通过。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 最终完整通过：C# 构建零警告零错误，项目导入和启动场景通过，279 项 .NET 测试及 220 项 Godot xUnit 测试通过；动画、角色管理、地图分块、图鉴、指针、背包与 GM、物品资源、移动、装备、丢弃、拾取、界面边界和角色设置全部通过。
- 首轮完整检查在既有指针光圈淡出断言处失败；未修改代码，单独运行该场景通过，随后完整重跑通过。保留这次瞬态失败记录，未扩大本次重构范围。
- `openspec validate flatten-server-role-cache --strict` 通过。本次内部重构不改变规范行为，使用 skip_specs，无需同步规范。
- 协议与四表存档结构保持原格式；本次未进行 Windows 导出验收。
