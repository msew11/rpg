## 检查结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 通过，C# 编译、Godot 导入和主场景启动正常。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过，.NET、Godot xUnit 与场景验收均通过。
- 新增测试覆盖锚点开关默认值、布尔解析、跨实例保存、保留其他键与合并重复键、写入失败保持状态；地面物品场景覆盖 GM 切换、失败恢复与重启后角色及物品标记恢复。
