# 验证记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：构建、资源导入、主场景、普通 .NET 与 Godot xUnit 用例通过；Godot xUnit 58 项无失败。
- `tests/actor_animation_smoke.tscn`：26 个动作、605 帧、八方向通过；新增男女站立、跑步首末帧头发像素世界坐标及组合 Anchor 回归检查，覆盖漏导头部、忘记裁剪偏移与世界定位偏移。
- `tests/client_smoke.tscn`：选角与客户端流程通过；日志中的模拟启动失败是既有故障恢复场景的预期输出。
- OpenGL 实际渲染男女八方向跑步预览，核对头身连接、镜像与脚下定位；另核对全部动作五个原始方向的首末帧拼图。
- 连续执行完整导入，全部图集、原生资源和来源记录的 SHA256 一致。
- 最终 `tools/check.ps1` 再次通过；中间帧目录包含 `.gdignore`，不参与 Godot 导入及导出。
- `openspec validate add-default-role-head --strict` 通过。

当前仅使用固定默认头型及自带头发，未提供运行时外观选择。原始素材保持只读，运行不依赖外部目录。
