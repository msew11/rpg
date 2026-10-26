# 验证记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`通过：C#构建零错误零警告、项目导入与启动、209个.NET测试、206个Godot xUnit测试及全部场景回归通过。
- EquipmentSmoke通过真实Ctrl＋左键验收主手非拳套时选主手、主手拳套且副手分别为盾牌/拳套/空槽时选副手；验证主手实例保留、旧副手回来源格、确认前缓存不变、没有世界移动。
- 既有手动放槽、错误槽位、满包拒绝、持久化和小视口验收继续通过。
- 变更OpenSpec严格校验及git diff --check通过。

限制：本次只调整快捷选槽，不增加装备属性或双手占用限制。
