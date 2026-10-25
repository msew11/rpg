## 检查结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：C# 构建、Godot 导入与启动、139 项 Godot xUnit 以及全部场景验收。
- `GroundItemDropSmoke` 验证真实信息框字号为 16、高度不超过 30 像素、悬停尺寸稳定；固定锚点的三个框优先沿中心线以 2 像素间距堆叠。
- 原有地面物品信息框无重叠、点击拾取和 UI 边界验收继续通过。
