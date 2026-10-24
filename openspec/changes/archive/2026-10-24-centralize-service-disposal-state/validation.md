# 验证记录

- dotnet test：73 项通过。
- 新增真实服务的接口状态回归，验证七个服务初始化、整体释放和重复释放；既有网络关闭失败及设置保存失败测试新增最终状态断言。
- tools/check.ps1：构建零警告零错误，Godot 导入及主场景通过。
- client_smoke、map_chunk_smoke、actor_animation_smoke 均通过；客户端包含预期启动失败清理。
- 七个服务没有重复声明 _disposed 或 IsDisposed，只有 Service 保存公共状态；Session 的网络与代次校验仍单独保留。
- 未增加释放钩子或更改具体服务标记状态与清理资源的顺序。
