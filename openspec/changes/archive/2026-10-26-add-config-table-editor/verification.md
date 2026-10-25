## 验证

- Edge headless：地图配置展开为三行，地图字段 `rowspan=3`；修改第二条 overrides 的 x，再重命名为 coordX，下载的 JSON 与 Schema 均正确更新。
- Edge headless：基础配置添加布尔字段并保存；道具配置显示七条记录，可添加第八条并填写可选字段。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1`：C# 构建、Godot 项目导入及主场景启动通过。

## 限制

页面只检查常见的类型、必填、数值范围、字符串长度、正则及数组最小长度约束；复杂 JSON Schema 关键字继续由游戏启动时的校验处理。内置快照只反映生成页面时的数据，编辑当前文件应先打开对应 JSON 和 Schema。
