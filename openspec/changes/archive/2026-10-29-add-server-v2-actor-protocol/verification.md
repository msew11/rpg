# 实施验收

2026-10-29 在现有 develop 完成，无新增分支或 worktree。

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：退出码 0，All checks passed；项目构建 0 警告、0 错误；.NET 测试 336 通过，Godot xUnit 282 通过，全部既有场景冒烟验收通过。
- 新增 `ServerV2ActorProtocolTests` 共 7 个验收用例：真实 Role→World 查询及存档字节/修改时间保持、目标 await 成功与失败时源邮箱串行且其他角色继续、请求与回复原实例直达和错误回复类型恢复、三类 Context 的服务定位隔离与借用生命周期、显式工厂控制、四种内部消息的客户端拒绝和默认登录未实现。目标 INIT 失败传播也由真实未知地图场景覆盖。
- 既有配置非空/释放状态、World 加载与内容比较、分块写回、失败重试及关闭补写验收全部保留并通过。
- `dotnet msbuild rpg.csproj -target:Build -getItem:Compile,Protobuf -verbosity:quiet -nologo`：内部两个源均使用 ProtoRoot=protov2/actor、独立 OutputDir、GrpcServices=None；Role.cs/World.cs 的 Compile FullPath 各出现一次；公共生成目录无内部项。生成 C# 的完整消息名和命名空间由运行验收验证，生成目录各 C# 的 Godot UID 完整。
- `openspec validate add-server-v2-actor-protocol --strict`：通过。
- 主规格同步后 `openspec validate --specs`：35 项通过、0 失败；其他既有规格的 Purpose 占位/过短及需求过长警告保持原样。本次两份相关主规格分别通过 `--type spec --strict` 校验。
- `git diff --check`：通过。

当前只提供进程内只读示例；协议对象独占移交由调用约定保证，禁止自身 Ask 和循环等待，不包含死锁检测、正式认证、通知框架、跨 Actor 事务或客户端默认入口切换。
