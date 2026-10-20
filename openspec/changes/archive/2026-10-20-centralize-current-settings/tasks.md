# Tasks

## 1. 设置仓库与数据类型

- [x] 1.1 将数据类型拆到 `DataSystemSetting.cs`、`DataRoleSetting.cs`，删除旧文件；验证项目可编译且 JSON 契约不变。
- [x] 1.2 让 `RepoSettings` 持有当前系统和角色设置、集中更新与保存；验证设置单测覆盖角色隔离、重启读取和拖动延迟保存。

## 2. 界面接入

- [x] 2.1 修改 `MainController` 启动和选角流程，并让 `GameHud` 只通过仓库读取和修改设置；验证客户端冒烟测试涵盖显示、声音、技能栏及 HUD 拖动。
- [x] 2.2 更新架构文档中的设置生命周期描述；验证与实现和 `client-settings` 规范一致。

## 3. 集成检查

- [x] 3.1 运行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1` 和 OpenSpec 校验，确认全部通过。

## Workflow follow-up

- 完成检查后归档 change，并将结果提交到本地 `develop`。
