## ADDED Requirements

### Requirement: 离线查看配置表

开发者 SHALL 能双击静态 HTML，在无网络环境中查看内置配置快照，并打开本地 JSON 及 Schema。

#### Scenario: 查看嵌套地图配置

- **WHEN** 开发者选择地图配置
- **THEN** 页面显示对象字段的多行分组表头，将 overrides 元素展开为内容行，并跨行合并同一地图的标量字段

### Requirement: 修改数据与结构

开发者 SHALL 能修改值、增删记录及数组子项，并对对象字段执行添加、重命名、删除且同步 Schema。

#### Scenario: 修改嵌套字段

- **WHEN** 开发者修改 overrides 元素的字段或其值
- **THEN** 页面修改对应 JSON 路径及适用的 Schema 结构，不影响相邻元素

#### Scenario: 保存文件

- **WHEN** 开发者保存 JSON 或 Schema
- **THEN** 页面分别保存对应文件；JSON 不符合页面支持的 Schema 约束时给出错误
