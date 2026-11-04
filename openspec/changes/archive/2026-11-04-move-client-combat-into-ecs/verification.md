# 验收记录

2026-11-04，Windows / Godot 4.7.2 .NET。

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 全部通过：构建零警告/错误，项目导入和启动通过，普通 .NET 154 项、Godot xUnit 315 项，无失败或跳过，全部场景通过。
- 新增 `WorldCombatTests` 九项：无服务走近和双位置范围检查、等待响应及拒绝重试、取消冷却及迟到确认、即时取消/停止顺序、权威时序与起手属性快照、远端控制版本及完整动作、怪物生命/死亡旧帧和致死跳字、全量重同步内部序号重置、换装取消与冷却。
- 既有男女空手/仅盾/六类武器真实输入、连续攻击、取消、法杖近战、时长缩放和致死跳字通过；真实 TCP 双进程的远端攻击、伤害、离开和重入通过。装备、移动、拾取及其他场景通过。
- `openspec validate move-client-combat-into-ecs --strict` 通过；`openspec validate --specs` 46 项通过。已有主规格 Purpose 占位/过短及长需求警告保留，没有修改无关文档。
- 差异确认：WorldService 无攻击运行状态和 CombatPose；系统不引用网络/配置服务；服务端模块及协议没有变更；怪物生命仅接纳权威结果。
- 本次运行自动化 headless 检查，没有新增 Windows 导出或图形窗口目视验收。
