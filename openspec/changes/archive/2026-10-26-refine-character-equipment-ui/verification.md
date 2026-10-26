# 检查记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：209 个普通 .NET 测试、206 个 Godot xUnit 测试与全部场景验收通过；编译零警告、零错误。
- 装备场景通过真实鼠标事件验证：预览原始比例、隐藏头像、十一空槽内灰色名称、图标旋转、Ctrl 自动选择主副手、拳刃推荐主手、替换回来源格、确认前缓存不变、自动卸下、右键无操作、满包拒绝和世界移动输入隔离；保留原七件装备、双拳刃、普通两次点击与持久化验收。
- 角色管理场景验证选角和创角原有头像、2.4 倍人物、统一图标、八方向回环、头型选择及创建删除流程没有回归。
- `godot-mono --path . res://tests/equipment_smoke.tscn -- --capture-equipment` 图形模式通过，人工核对 `build/equipment-window.png`：装备窗口人物与地图等大，槽内灰色部位名及箭头图标清晰，480×270 下仍可滚动到鞋子槽。
- `openspec validate refine-character-equipment-ui --strict`、主规格检查（28 通过，0 失败）及 `git diff --check` 通过。主规格检查存在其他既有 Purpose/长度警告。

## 行为边界

快捷操作沿用既有装备槽位映射与服务器权威规则，拳刃默认主手，手动放置仍可选择副手。选角与创角继续显示头像及放大人物，装备窗口使用无头像的原始比例。截图与检查日志位于 build，不提交。
