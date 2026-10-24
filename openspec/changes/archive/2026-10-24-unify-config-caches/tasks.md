## 1. 统一缓存

- [x] 1.1 实现文件/主键特性、单例与多例基类、统一校验加载及生命周期。
- [x] 1.2 迁移 MapCfgCache、ItemCfgCache、ConfigService 和 JSON/Schema，平铺 config/ 并拒绝子目录，更新全部消费者。

## 2. 验收交付

- [x] 2.1 迁移现有配置验收，新增通用主键、标注错误、根形状、加载失败与缓存生命周期回归。
- [x] 2.2 更新规范文档和路线图，通过完整项目检查及规格校验，完成归档与本地提交。

## 验证结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：构建零警告/错误，36 个普通 .NET 用例、108 个 Godot 用例及全部四项场景验收通过。
- 新增 17 个通用缓存验收，验证字符串主键及字段别名、单例/多例分流、根与行形状、主键和文件标注错误、读取/Schema 失败、地图跨字段约束及借出缓存释放。
- 迁移已有 19 个道具配置验收及地图消费者；验证平铺目录、缺表、子目录拒绝、重复主键、空表与只读映射。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -ExportWindows` 通过：配置与 Schema 平铺导出，Windows 程序启动及 ZIP 打包成功；输出仅位于忽略的 build/。
- change 严格校验通过；全部 17 项主规格通过校验，已同步新增/修改的三个能力。
- 正式道具数据仍为 []，分类含义及具体数值待确认。
