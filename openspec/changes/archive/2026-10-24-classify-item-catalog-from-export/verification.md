# 验收记录

- 核对指定 `item_export/index.html` 内嵌全部21,792条记录与同目录 `items.json` 字段一致；项目内导入记录逐字段比较全部一致，来源行唯一，候选路径精确命中图集，来源文件SHA256一致。
- 六父类型数量与页面一致：武器879、防具／饰品8,286、其他装备30、宠物1、收集品1,073、消耗品11,523。保留477条缺图、21,315条候选及ID13541、13542、8014的重复行。
- `powershell -ExecutionPolicy Bypass -File tools/test.ps1` 通过项目编译、导入与正式启动、28个普通.NET用例、72个Godot用例和动画、角色管理及道具图鉴场景。
- 扩展图鉴验收检查默认六父类型、全部子类型名称和数量、48条分页、名称/ID/说明搜索、ID24618项链误分类回归、图标精确区域、重复行展示、缺图详情、素材模式、关闭重开与小窗口布局。
- Windows D3D12图形模式运行同一验收场景通过，检查 `build/item-catalog-classified.png` 与 `build/item-catalog-necklaces.png`，父子类型、具名条目、候选图像、滚动详情和操作控件均可见。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -ExportWindows` 通过导出、导出程序启动及ZIP打包；导出日志包含 `definitions.json` 与 `definitions_source.json`，运行资源不依赖外部清单。
- 装备和子类型语义沿用来源推定，图标保留候选性质；未新增装备玩法，也未将缺图道具过滤。
