# 验收记录

- 原素材核对：四目录 PNG 与目录条目集合完全相等；22,872 条来源 SHA256 与无损图集裁剪像素逐一比较全部一致。
- `powershell -ExecutionPolicy Bypass -File tools/test.ps1` 通过：编译零警告、正式导入与启动、28 个普通 .NET 用例、72 个 Godot xUnit 用例、身体动画、角色管理及新增道具图鉴场景。
- 图鉴验收覆盖全部区域合法性、四分类计数、首尾条目检索、48 条分页与往返、空结果、详情纹理及来源、真实 GM 按钮、关闭重开、小窗口和点击不触发移动。
- Windows D3D12 图形模式下运行同一图鉴场景并截图复核，确认不透明面板遮挡 HUD，缩略图、分类、搜索、翻页与详情可用。测试发现内容更新导致容器暂时撑高，已由稳定布局后的视口约束修正。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -ExportWindows` 通过 Windows 导出、导出程序启动与 ZIP 打包；导出日志确认包含 `res://assets/items/catalog.json`。
- 素材来自已解码 PNG 首帧；没有正式道具名称、职业与装备类型表，按四个来源分类保留编号，不推断玩法语义。
- 新 change 与归档后的 `item-catalog` 规范严格校验通过。额外执行全库严格校验时，原有 `main-branch-scope` 与 `windows-export-check` 因 Purpose 少于 50 字符产生警告；这两份文件未改动，其余 13 份规范通过。
