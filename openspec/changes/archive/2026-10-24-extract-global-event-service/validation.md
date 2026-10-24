# 验收结果

- `dotnet test tests/ClientTests/ClientTests.csproj --nologo`：72 项通过。
- `tools/check.ps1`：构建零警告、零错误，Godot 导入与主场景检查通过。
- `client_smoke.tscn`：通过，包含预期的模拟注册后启动失败及清理验证。
- `map_chunk_smoke.tscn`：通过。
- `actor_animation_smoke.tscn`：通过，26 个动作、605 帧、8 个方向。
- 新增回归验证多个网络共用事件域、网络不拥有监听错误及事件生命周期；架构检查限制唯一创建位置和无公开构造函数；原生命周期测试补充旧事件引用、旧订阅和旧句柄对重启的隔离。
- 本次只重构事件服务，不新增协议或游戏行为。
