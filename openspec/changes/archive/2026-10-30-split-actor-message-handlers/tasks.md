# Tasks

## 1. 消息处理器结构

- [x] 1.1 拆分 Login、Role、World 处理器，提取共用辅助类到 Common/Utils 或各模块 Utils 并更新注册；同步 AOI 测试、增加结构约束回归及更新架构和路线图，通过编译与处理器结构验收确认目录、命名、单契约和原消息覆盖。

## 2. 集成检查

- [x] 2.1 执行 tools/check.ps1 与相关业务回归，确认物品、移动和 AOI 保持原行为，并通过 OpenSpec 严格校验。

验证记录：`powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 全部通过；C# 零警告零错误，普通 .NET 138 项、Godot xUnit 267 项及全部场景验收通过。新增两项结构回归验证单请求契约、独立文件、处理器目录纯净和 Utils 的 Actor 边界。`openspec validate split-actor-message-handlers --strict` 通过。

## Workflow follow-up

- 完成检查后同步规范并归档 change，提交中文约定式本地提交。
