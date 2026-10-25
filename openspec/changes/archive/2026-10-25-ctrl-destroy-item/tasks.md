## 1. 实现

- [x] 1.1 扩展销毁协议、Session 与服务端权威扣除，保持普通丢弃和落地边界。
- [x] 1.2 实现 Ctrl 点击地图的明确数量确认与模式冻结，单件必须确认。

## 2. 验收与交付

- [x] 2.1 补充协议及真实输入测试，验证取消、单件、多件、非法/重放和重启。
- [x] 2.2 更新路线图与架构说明，运行 tools/check.ps1 -Tests，校验并归档 change。

## 检查结果

- powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests：通过。
- pwsh -NoProfile -ExecutionPolicy Bypass -File tools/check.ps1 -Tests：通过全部场景，.NET 175 项、Godot xUnit 139 项均无失败。
- 真实输入丢弃场景验证 Ctrl 确认/取消、单件、部分/全部、模式冻结、地面不新增及关闭重启；普通移动、丢弃和拾取回归通过。
- openspec validate ctrl-destroy-item --strict：通过。
- ground-item-drop 主规格严格校验通过；全仓规格严格校验有四项既有 Purpose 过短警告（gm-item、main-branch-scope、role-bag、windows-export-check），已在未修改的主工作树确认相同结果。
