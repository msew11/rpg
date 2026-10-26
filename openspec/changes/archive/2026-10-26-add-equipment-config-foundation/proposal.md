# Proposal

## Why

道具分类当前只是整数，缺少可校验的装备配置关联和分类索引。先补齐配置基础，供后续装备功能使用，当前不实现穿脱、消息、存档或角色窗口。

## What Changes

- 将道具大类、小类和角色装备槽位枚举化，启动拒绝未定义值及不匹配的大小类组合。
- 仅以代码绑定武器/剑到主手，装备表不配置槽位。
- item.json 增加 relatedId，新增 equipment.json、EquipmentCfg 和对应缓存；全部装备类道具必须引用存在的装备记录。
- CfgCache 提供加载后索引构建及 Cache 级关联检测扩展点，ItemCfgCache 构建大类及大小类双键索引，值为 List<ItemCfg>。
- 沿用 Validate，通过整表重载在全部表完成加载和索引后统一检测；将已有初始地图引用检测迁入该机制。
- **BREAKING**：原先任意非负分类整数不再合法，配置目录必须包含 equipment.json，道具记录必须包含 relatedId。

## Capabilities

### New Capabilities

- `equipment-config`: 装备表引用完整性、槽位枚举及剑到主手的代码映射。

### Modified Capabilities

- `client-item-config`: 枚举分类、关联 ID 与分类索引。
- `client-config-cache`: 加载后索引钩子、关联校验重载及装备缓存生命周期。

## Impact

涉及 src/Const、src/Config、槽位规则、config、schemas、配置测试和既有隔离配置夹具、离线配置页示例及架构/路线图文档。保留七件测试装备；不修改运行时默认武器或实现穿戴。
