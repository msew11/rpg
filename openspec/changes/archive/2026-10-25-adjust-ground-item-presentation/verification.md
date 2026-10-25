## 检查结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：C# 构建、Godot 导入与启动、139 项 Godot xUnit、全部场景验收。
- `GroundItemDropTests` 覆盖近距离随机落点、地图边界、身体避让及小数坐标。
- `GroundItemDropSmoke` 覆盖角色与物品共用 Y 排序、物品真实锚点、GM 显隐及改色；既有丢弃、拾取和 UI 场景继续通过。

## 限制

- GM 锚点颜色和开关仅在当前世界会话有效，不持久化。
- Windows 图形窗口中的遮挡视觉效果仍建议人工目视确认。
