# 验证记录

- `powershell -ExecutionPolicy Bypass -File tools/test.ps1` 通过，包含完整 check、构建、导入、正式启动、167 项普通 .NET、139 项 Godot 测试及全部冒烟场景；构建无警告和错误。
- 拾取冒烟通过真实输入验证透明区域和扩展区域高亮、扩展区域点击拾取、区域外复原、名称、Fetch、UI/模态/失焦隔离及原拾取闭环。
- 实际显示纹理与 GroundAssetId 解析结果一致，并且与背包纹理不同。
- 配置测试验证两套素材路径、可选地面字段及非字符串拒绝。
- 七件配置的地面素材均按 item_export/items.json 的 ov_arm.bin 对应行 icon_small_code 绑定，图集内均存在；没有重新导入素材。
- 严格 change 校验通过，全部 25 项主规格校验通过。既有其他主规格的 Purpose/长描述警告保留。
- 仅放宽客户端命中；服务端拾取距离、存档和协议不变。命中框重叠时仍按最上层优先。
