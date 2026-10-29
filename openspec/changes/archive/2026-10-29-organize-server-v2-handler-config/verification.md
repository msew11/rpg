# 验收记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：330 项 .NET 测试、229 项 Godot 测试，以及全部真实场景验收。
- V2 普通定向测试 51 项通过；新增未装配配置时拒绝读取，避免隐式回退到全局服务。
- 新增 Godot 测试验证三种 Actor 的 Handler 通过 Context.Config 读取同一宿主的已加载配置；两个服务读取不同配置实例，停服不释放借用配置，配置所有者释放后上下文拒绝读取，另一服务继续工作。
- 内部装配测试验证 MockServerService 拒绝缺失配置及其他配置实例的模块。
- 当前 change 与 server-v2-account 主规格的严格校验通过；默认登录继续返回 NotImplemented，旧客户端链路通过全部场景验收。
- 已核对 Handler 及 uid 的目录迁移、Context.Config 入口、配置只读使用与借用生命周期。旧 Server 和角色创建 change 的未提交改动不纳入本次提交。
