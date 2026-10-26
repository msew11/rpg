# 验证记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：构建零警告、零错误，209个.NET测试、206个Godot xUnit测试及全部场景验收通过。
- `EquipmentSmoke`：七件装备从背包与装备拿起时比较实际图像尺寸、像素、数量、采样及透明度；验证中心吸附、隐藏背包时即时名称、空槽/移出/拿起/关闭清理；装备窗口默认右侧身与顺逆旋转通过。
- `RoleManagementSmoke`：创角默认右侧身、按钮对应实际方向、独立性别草稿记忆、选角显示与既有八方向回环通过。
- `ActorAnimationSmoke`：七件装备、男女13动作、八方向及全部身体帧挂点通过，后段随帧/镜像同步、空手清理通过；双手剑与拳刃按原始素材不透明颜色和全部alpha的黄金哈希验收。
- Windows D3D12图形模式运行 `actor_animation_smoke.tscn -- --capture-actors`：原长剑遮挡回归及枪杖站立、跑步八方向实际渲染通过；八组长柄后段身体交叠检查像素数为1480、1092、792、871、1240、1126、1016、1118。
- 导入七件装备1675帧，枪杖430帧逐帧校验前后段无损重组、不重复像素；运行仅依赖项目资源。
- `openspec validate fix-equipment-presentation --strict`、`git diff --check` 通过。

限制：原素材缺失的动作继续按source.json显式复用；道具信息本次只展示名称，后续字段通过共用入口扩展。
