# Tasks

## 1. 统一模块装配

- [x] 1.1 将三类正式 Handler 固定注册在 ServerModules 构造方法内，移除工厂参数并同步 MockServer；隔离现有自定义测试装配、更新结构验收和架构文档，通过生产与测试编译核对所有调用点。

## 2. 集成验证

- [x] 2.1 运行 tools/check.ps1 -Tests 与 OpenSpec 严格校验，确认正式业务、保存调度及自定义边界验收全部通过。

验证记录：C# 生产与测试程序集零警告、零错误；`powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 全部通过，普通 .NET 138 项、Godot xUnit 267 项及全部场景验收通过。既有结构回归补充正式构造器不接收处理器工厂的检查；默认内部跨 Actor 查询继续直接验证正式 ServerModules。`openspec validate centralize-server-handler-registration --strict` 通过。

## Workflow follow-up

- 完成检查后归档 change，创建中文约定式本地提交。
