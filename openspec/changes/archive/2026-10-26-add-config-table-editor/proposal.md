## Why

目前 JSON 配置及 Schema 需要分别查看和修改，嵌套数组在文本中不易对照字段关系。开发期需要一个可离线打开的表格页面。

## What Changes

- 在 `config/index.html` 提供静态配置表编辑页，内置当前配置快照，并可打开本地 JSON 与 Schema。
- 按 Schema 生成分组表头，将嵌套数组展开为内容行并合并相同父级单元格。
- 支持值、记录、子项及字段结构编辑；字段增删改名同步数据与 Schema，并分别保存两种文件。

## Capabilities

### New Capabilities

- `config-table-editor`：本地配置查看、编辑与导出。

## Impact

仅增加开发工具页面与能力文档，不改变游戏运行时配置格式及加载路径。
