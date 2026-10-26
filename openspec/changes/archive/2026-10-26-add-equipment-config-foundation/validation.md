# 验证记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 最终完整通过：C# 编译零警告零错误、Godot 导入及启动、205 个普通 .NET 用例、199 个 Godot xUnit 用例和全部既有场景验收。
- 新增验收覆盖未定义枚举、缺失/null 分类及关联字段、大小类错配、大小类 List 索引的多条记录/顺序/副本隔离/释放、空表和非法装备表、武器与防具引用缺失、不同道具 ID 共享装备配置、索引构建失败清理及重试，以及仅剑类绑定主手。
- 正式七件样例遵循装备与道具 ID 默认同值的编写约定；共享引用用例明确验证不强制同值。
- 首次完整检查曾在既有地面丢弃场景出现“离开 AOI 未移除地面表现”；单独复跑及后续两次完整检查均通过，未改动 AOI 或表现逻辑。
- `openspec validate add-equipment-config-foundation --strict` 和 `git diff --check` 通过。
- 配置页四张表内置示例与正式 JSON、Schema 同步；未添加穿戴消息、UI、装备实例或新属性，现有固定长剑展示保持原样。
