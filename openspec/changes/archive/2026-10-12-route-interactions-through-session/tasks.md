# Tasks

## 1. 会话入口与界面

- [x] 1.1 在 `LocalSession` 封装所有当前生产世界交互并返回命令结果；用会话交互测试验证地块、调试、怪物和移动行为。
- [x] 1.2 让 `Main` 和 `DebugPanel` 通过当前会话提交交互；用快捷键、面板、新游戏重新绑定与纯 UI 场景验证既有行为。
- [x] 1.3 更新架构和路线图，检查生产 UI 不再调用 `WorldManager.send_event()` 且文档与代码一致。

## 2. 集成检查

- [x] 2.1 运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 并处理失败项。

## Workflow follow-up

- 检查完成后同步规格并归档 change。
