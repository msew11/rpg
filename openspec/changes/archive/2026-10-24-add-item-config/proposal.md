## Why

当前 ConfigService 只支持整份地图配置，需要增加按道具 ID 查询的表格配置，为后续功能提供基础数据入口。

## What Changes

- 新增 item.json、配套 Schema 和四字段 ItemCfg，通过 ConfigService 构造加载。
- 将行记录按 id 存为只读 Map，校验字段、重复 ID、重复表及文件名。
- 初始表为空，不擅自确定正式道具、分类含义或堆叠数值；用测试数据验收有记录的加载。

## Capabilities

### New Capabilities
- `client-item-config`: 道具表校验、按 ID 读取和服务释放约束。

### Modified Capabilities

无；沿用现有客户端配置扫描约定。

## Impact

影响 src/Config、config、schemas、Godot 配置验收与架构及路线图。保持地图入口，图鉴资源不作为正式道具配置导入，不增加背包或协议。
