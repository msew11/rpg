# 验收记录

- Windows / Godot 4.7.2 .NET：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 完整通过；同样通过 PowerShell 7 的完整检查。
- C# 构建零警告、零错误；167 项普通 .NET 测试、139 项 Godot xUnit 测试通过，11 个场景验收通过。
- 新增 RoleSettingsSmoke：真实标题拖动三个窗口、松开保存、关闭重开、重启恢复与角色隔离；系统/角色页签分类；点击高亮、Esc/方向键/单独修饰键/系统组合键/重复绑定被拦截且持续等待；合法 Ctrl+R 保存并即时生效；切页和关闭取消；重复事件与聊天输入隔离。
- 新增回归同时覆盖快捷键写盘失败后保留原绑定并高亮重试、旧文件回填、无效及重复存档绑定回退，以及小视口裁剪。
- 原背包、道具移动/丢弃/拾取、HUD 保存/取消/恢复和视口边界场景继续通过；角色管理验证离开角色后待释放节点不再读取已清空设置。
- `openspec validate role-interface-settings --strict` 与全部 25 份主 specs 校验通过；同步时保留已有 Purpose 与无关需求。
- 检查脚本加 UTF-8 BOM，保证 Windows PowerShell 正确读取中文注释并执行全部场景，新增场景纳入统一检查。
- 本轮使用 headless 场景验收，未执行 Windows 导出或人工图形验收。
