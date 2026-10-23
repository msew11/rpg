## 1. 服务与节点重构
- [x] 1.1 实现统一服务接口、各业务服务及整体注册生命周期，迁移节点业务持有和协调逻辑。

## 2. 验证与交付
- [x] 2.1 补充服务生命周期和状态隔离测试，迁移并运行现有单元、Godot 场景及 tools/check.ps1 检查。
- [ ] 2.2 更新架构与路线图，同步 specs 并创建中文本地提交。

## 验证记录

- `dotnet test tests/ClientTests/ClientTests.csproj --nologo`：70/70 通过；新增覆盖非法登录摘要拦截、业务先于 UI 就绪、切角设置隔离，以及网络或设置释放异常后的其他服务清理。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1`：构建、Godot 资源导入和主场景启动通过。
- `client_smoke.tscn`、`map_chunk_smoke.tscn`、`actor_animation_smoke.tscn`：通过；客户端场景包含预期的受控启动失败，动画场景覆盖 26 个男女动作、605 帧和八方向。
- change 与 `game-services` spec 严格校验通过。全量严格校验的两个既有警告来自 `main-branch-scope` 和 `windows-export-check` 的 Purpose 少于 50 字符，相关文件未修改。
