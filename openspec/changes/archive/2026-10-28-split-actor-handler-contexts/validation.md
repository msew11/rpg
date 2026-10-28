# 验证记录

- `dotnet test tests/ClientTests/ClientTests.csproj --nologo --filter "FullyQualifiedName~ServerHandlerTests|FullyQualifiedName~ServerActor"`：16 项通过。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：退出码 0；272 项 .NET 测试通过，Godot xUnit 220 项通过，项目导入、启动及全部场景验收通过。
- `openspec validate split-actor-handler-contexts --strict --json`：通过；内部重构声明 `skip_specs: true`，没有规格差量。
- `git diff --check`：通过。

新增 Context 缓存绑定测试；扩展 Handler 签名测试，验证每个 Handler 由所属 Actor 以只读字段声明且只接收对应 Context。新增地图事务失败回归，验证 WorldActor 路由选角及 AOI，角色工作副本的移动队列和 AOI 修改不会泄露回 RoleActor。已有锁文件直接处理、无效请求原子拒绝、走近拾取、分片失败重试、物品共同落地和摘要同步由完整套件覆盖。

架构与路线图已按最终 Context、Handler 归属更新。Actor 继续使用独立异步邮箱回合；多客户端和跨地图迁移保持待定。
