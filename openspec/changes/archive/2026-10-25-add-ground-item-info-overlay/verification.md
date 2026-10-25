## 检查结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：C# 构建、Godot 导入与启动、139 项 Godot xUnit 以及全部场景验收。
- `GroundItemDropSmoke` 验证默认无常驻数量、Z 显示单件/堆叠信息、信息框不重叠、悬停高亮与点击拾取不触发地图反馈。
- `RoleSettingsSmoke` 验证默认 Z、改绑持久化、聊天输入隔离，以及旧角色已占用 Z 时保留原绑定。

## 限制

- 视口无法放下所有信息框时，无法无重叠放置的框隐藏；地图物品仍可通过图标悬停和点击拾取。
