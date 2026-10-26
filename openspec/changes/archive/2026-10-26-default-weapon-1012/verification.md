# 检查记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：通过。C# 编译无警告、无错误，项目导入、主场景、普通 .NET 与 Godot 测试、全部场景回归通过。
- `godot-mono --path . res://tests/actor_animation_smoke.tscn -- --capture-actors`：通过，无引擎错误；目视核对男女站立/跑步八方向，剑柄与手部对齐，前后关系随帧切换。
- 新增外观验收：26 个身体动作、605 帧挂接点、320 帧武器，男女全部动作/方向/帧有纹理；原始黄金坐标、镜像改向保持进度、跑步前后遮挡、停止恢复站立及不提高跨实体 Z 值。
- 武器导入重跑后 29 个 PNG/TRES/JSON 文件 SHA256 一致；运行只加载项目内资源。
- `openspec validate default-weapon-1012 --strict` 与 `git diff --check`：通过。
- 本地预览为 `build/weapon-1012-preview.png` 和 `build/weapon-1012-preview.gif`，不提交 build 输出。

## 限制

1012 作为默认外观显式绑定基础剑系列 weaponr01，不将物品编号直接当作原始武器文件编号。原素材只提供男女七类独立长剑动作，其余动作复用已记录在 source.json 和架构文档中；本次未新增背包装备及属性规则。
