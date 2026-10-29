# 验证记录

- RoleGenderPb 和 RoleSummaryPb 已并入 protov2/login.proto，放在 Login/LoginRt 后；当前没有推送，保留以后放在请求响应和辅助类型之间的顺序约定。
- 移除 role_summary.proto、旧 import、RoleSummary.cs 及其 UID。Login.cs 由 Grpc.Tools 重新生成，生成内容未手工修改。
- 迁移前后的枚举和摘要结构原文比较一致；消息名称、字段号、类型及枚举数值没有变化，反射所属文件改为 login.proto。
- 当前源码、测试和文档无 role_summary.proto 或 RoleSummaryReflection 残留引用。
- `dotnet build rpg.csproj --no-restore --nologo`：0 警告、0 错误。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：退出码 0，All checks passed；307 个 .NET 测试、258 个 Godot xUnit 测试及全部 smoke 场景通过，含既有登录摘要编解码验收。
- `openspec validate merge-server-v2-login-protocol --strict` 与 `git diff --check`：通过。
- V2 协议组织文档已同步；按会话要求合并到上次本地提交，不推送。
