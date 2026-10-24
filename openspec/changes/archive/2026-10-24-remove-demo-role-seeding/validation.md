# 验证记录

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：编译零警告、资源导入与主场景、36 项普通 .NET 测试、72 项 Godot xUnit 测试和角色管理 UI。
- 单独运行 actor_animation_smoke、client_smoke、item_catalog_smoke 和 pointer_feedback_smoke，全部退出码 0。client_smoke 中的模拟启动失败为既有预期故障场景。
- 新增 8 项仓库回归：全新目录重复/重启为空、三个缺失编号不生成、男女首次创建/墓碑递增、两种旧标记状态保留已有存档。
- 原有外观贯通协议验收改从空存档登录，创建首个男女角色后验证选角、ECS、移动和重启；真实 UI 验收从空列表创建首个角色。
- 已有存档数据、协议、认证和新角色初始属性不变。不新增注册或多账号。
- 所有修改位于独立 worktree；当前工作区未修改、未合并、未推送。
