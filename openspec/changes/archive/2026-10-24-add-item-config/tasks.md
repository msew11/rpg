## 1. 配置加载

- [x] 1.1 添加 ItemCfg、空 item.json 与 Schema，接入 ConfigService 按 ID 只读映射及错误诊断。

## 2. 验收与交付

- [x] 2.1 增加 Godot 验收，覆盖多行、空表、无效数据、文件契约与生命周期。
- [x] 2.2 更新架构及路线图，运行项目检查与测试，校对并归档 change，完成本地中文提交。

## 验证结果

- `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 通过：构建零警告/错误，项目导入和主场景正常，36 个普通 .NET 用例、91 个 Godot 用例及全部四项场景验收通过。
- 新增 19 个 Godot 道具配置用例，覆盖服务入口读取、空表、只读与释放、字段校验及文件契约。
- `openspec validate --specs`：16 项通过；change 严格校验通过。
- 正式道具表为空，分类含义与具体道具数值待确认。
