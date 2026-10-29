# 验证记录

- 已删除 DataAccountRoleSummaries 及其 Godot UID，源码、测试和当前文档中无引用。
- 摘要仓库直接加载 ImmutableArray<DataRoleSummary>，保存参数为 username 和摘要数组。Actor 使用固定账号身份，快照无需包装/解包；JSON 数组格式保持不变。
- 新增真实仓库路径验收：非法账号在读写和创建目录前被拒绝；新增 default 数组保存为 [] 并重新加载的验收。
- 现有测试继续覆盖摘要缺失不写盘、坏 JSON/根 null 拒绝、不增加业务校验、内容比较、失败重试、重启恢复及关闭补写。
- `dotnet test tests/ClientTests/ClientTests.csproj --filter 'FullyQualifiedName~ServerV2&Runtime!=Godot' --no-restore -v minimal`：28/28 通过。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：退出码 0，All checks passed；307 个 .NET 测试、258 个 Godot xUnit 测试及所有 smoke 场景通过。
- `openspec validate remove-server-v2-account-summary-wrapper --strict`：通过，skip_specs，无行为规格差异。
- `git diff --check`：通过。文档已同步；按用户要求合并进上次本地提交，不推送。
