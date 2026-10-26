# 检查记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：204 个普通 .NET、174 个 Godot xUnit 及全部场景验收通过；编译零警告、零错误。
- `godot-mono --path . res://tests/actor_animation_smoke.tscn -- --capture-actors` 通过。GPU 实际渲染检查男女站立/跑步的正背面不透明交叠像素，各组合分别覆盖 336、256、184、248、1231、243、363、60 个样本，前景颜色均符合正面剑身、背面身体的要求。
- 新回归验证实际右手：男女八方向站立黄金坐标、跑步摆臂挂点、镜像不换手、全部 26 动作及 605 帧双手挂点、剑柄原点对齐、转向进度和停止、不改变实体 Z 值。
- 26 组身体 PNG/TRES 共 52 个文件重复导入，SHA256 一致；原头部资源保持不变。
- `openspec validate fix-weapon-right-hand-occlusion --strict` 和 `git diff --check` 通过。
- 人工核对四排男女八方向的站立与跑步预览，以及正背面放大图。预览保存在 `build/weapon-right-hand-preview.gif`、`build/weapon-right-hand-front-back.png`，不提交 build 输出。

## 限制

继续使用现有五方向武器及三个镜像方向，部分非移动动作沿用原有复用策略。右手定位由独立逐帧挂点完成，遮挡单独处理；没有新增装备或战斗规则。
