# 验收记录

- V2 定向测试：50 项通过。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：通过，包含 329 项 .NET 测试、227 项 Godot 测试，以及全部真实场景验收。
- 新增回归场景：Login 原实例和 username/password 完整到达账号 Handler，规范账号分片不变；LoginRt 原实例直接返回；显式回复类型错误后邮箱继续工作。
- 原有测试继续覆盖默认登录 NotImplemented、会话代次隔离、请求排空、周期/显式/关闭写回及旧客户端链路。
- 当前 change 与 server-v2-runtime 主规格的严格校验通过；全部 34 项主规格常规校验通过。额外全库严格校验有 11 项既有文档警告（Purpose 占位、过短或需求过长），涉及未修改的其他规格，本次保留。
- 已核对协议独占使用约定、存储模型独立与 Context/Actor/Repo 的原职责。原有旧 Server 改动与角色创建 change 不纳入提交。
