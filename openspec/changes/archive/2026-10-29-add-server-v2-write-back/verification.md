# 验收记录

- `tools/check.ps1 -Tests` 完整通过：普通 .NET 328 项、Godot xUnit 227 项，以及启动、动画、角色管理、地图、图鉴、指针反馈、背包、装备、丢弃拾取、UI 边界及角色设置场景。
- 本次新增 34 项验收：存储 20 项、Actor 5 项、宿主写回 4 项、Godot 保存调度与服务集成 5 项。
- 最终公共枚举目录调整后，重新通过全部 49 项 V2 普通测试和 227 项 Godot 测试。
- OpenSpec change、server-v2-account、server-v2-storage 严格校验通过；全部主规格普通校验通过，既有其他规格的占位 Purpose 等警告未在本次修改。
- 初次完整检查曾出现旧指针场景计时断言失败，后续尝试出现动画场景 Godot GCHandle 致命错误；单独复查及稳定完整运行均通过，未改动这些场景。
- 新目录为模块 Data/Storage 与 Modules/Common；不迁移旧文件，不实现认证、业务数据校验、World 存储或跨 Actor 事务。
