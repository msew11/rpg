# 验证记录

## 快照与写回

- Login 新增 DataLoginSnapshot(Summaries)，Context 统一使用 Snapshot()/Commit(...)，保存基线使用聚合快照。
- 扩充 server-v2-storage 的 Context 验收：旧快照不随提交改变、嵌套数组按内容比较、恢复原值取消待写、保存后接受新基线、空提交在修改前拒绝。
- 现有 Login 持久化验收继续覆盖首次加载、无变化不写文件、修改后保存与重启恢复；邮箱、保存失败重试和关闭补写验收通过。
- IAccountRoleSummaryRepo 与摘要数组 JSON 格式保持原有契约；没有新增正式业务协议或数据校验。

## 命令结果

- `dotnet test tests/ClientTests/ClientTests.csproj --filter 'FullyQualifiedName~ServerV2&Runtime!=Godot' --no-restore -v minimal`：24/24 通过。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests`：构建、项目导入、主场景通过；普通 .NET 303/303、Godot xUnit 257/257 通过。角色动画、角色管理、地图分块、物品图鉴场景通过。脚本在 pointer feedback 场景报“光圈未淡出或随摄像机漂移”而中断，完整检查未一次通过。
- 单独运行 `res://tests/pointer_feedback_smoke.tscn` 通过；该场景在此前 change 的检查中也有相同不稳定记录。
- 补跑尚未执行的 bag_gm、item_asset、item_movement、equipment、ground_item_drop、ground_item_pickup、ui_bounds、role_settings 共 8 个 smoke 场景，全部退出码 0，未报告 ERROR。
- `openspec validate add-server-v2-login-snapshot --strict`：通过。本次为结构重构，skip_specs，无行为规格差异。
- `git diff --check`：通过。源码、测试和当前文档没有 ReplaceSummaries 残留。

## 限制

默认登录仍为尚未实现；本次不改变认证或旧客户端链路。完整检查的指针场景不稳定问题未在本次快照重构中修改。
