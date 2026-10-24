## Why

用户需要将指定七件测试装备加入正式道具配置，以通过现有配置缓存按 ID 读取名称、分类和堆叠上限。当前表为空且缺少名称字段，需要同时补齐数据结构和首批可查询记录。

## What Changes

- 新增 ItemCfg.Name 与 JSON name，Schema 要求非空名称。
- 加入 1012—1018 七条用户指定记录，六件武器 category=1，盾牌 category=2，maxStack=1；小类沿用确认的源编号。
- 更新空表验收，增加打包配置的七条记录读取验收。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-item-config`: 道具记录包含名称，随程序提供指定七件测试装备。

## Impact

影响 config/item.json、ItemCfg、Schema、配置测试及架构和路线图；不增加穿戴、背包或协议行为。
