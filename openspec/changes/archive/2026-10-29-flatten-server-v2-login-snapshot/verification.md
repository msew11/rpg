# 验证记录

- DataLoginSnapshot.Summaries 与 LoginContext.Summaries 直接使用 ImmutableArray<DataRoleSummary>，账号身份由所属 Actor/Context 固定维护。
- DataAccountRoleSummaries 仅保留在现有仓库契约及 Actor 的 IO 边界。摘要数组文件格式保持不变。
- 快照隔离、嵌套数组内容比较、恢复保存基线、空快照拒绝、保存后重启恢复均通过；新增测试验证 default 数组归一为空表，恢复为空后取消待写。
- `dotnet test tests/ClientTests/ClientTests.csproj --filter 'FullyQualifiedName~ServerV2&Runtime!=Godot' --no-restore -v minimal`：24/24 通过。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：退出码 0，输出 All checks passed；303 个 .NET 测试、258 个 Godot xUnit 测试及所有 smoke 场景通过。
- `openspec validate flatten-server-v2-login-snapshot --strict`：通过。skip_specs，仅调整结构，无行为规格差异。
- `git diff --check`：通过；当前快照与 Context 不再引用仓库包装或 Summaries.Roles。
- 文档已同步；按用户要求合并入上次本地提交，不推送。
