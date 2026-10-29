# Tasks

## 1. 冗余引用清理

- [x] 1.1 定向清理手写 C# 的未使用 using 与冗余名称限定，检查差异及诊断复查，确认生成代码和行为不变。

## 2. 集成验证

- [x] 2.1 运行 tools/check.ps1 -Tests，记录结果并核对长期文档与路线图无需变更。

## 验证记录

- 32 个手写 C# 文件移除 73 条未使用 using，简化 22 处冗余写法；protobuf 生成文件、编辑器配置及长期文档无变化。
- 临时启用 IDE0005、IDE0001 后执行 `dotnet format style rpg.sln --diagnostics IDE0005 IDE0001 --severity info --no-restore --exclude src/Net/Message --verify-no-changes`，退出码 0，报告为空；检查后恢复原编辑器配置。
- Rider 复查全部 11 个 Handler，未发现未使用 using 或冗余限定诊断。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 最终完整通过：279 个普通 .NET 用例、220 个 Godot xUnit 用例和全部场景检查。
- 首轮光圈时序断言失败，单独复跑及第二轮完整检查通过；本次不改变光圈实现或时序断言。
- `git diff --check` 通过；产品方向、模块边界、阶段顺序均不变，长期文档与路线图无需调整。

## Workflow follow-up

- 检查完成后归档 change，并按约定式提交保留本地提交。
