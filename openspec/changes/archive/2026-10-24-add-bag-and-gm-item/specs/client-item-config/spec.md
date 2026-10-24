## ADDED Requirements

### Requirement: 道具图标配置
ItemCfg SHALL 支持可选字符串 IconAssetId，缺失时为空；非字符串 SHALL 被 Schema 拒绝。正式七件测试装备 SHALL 显式绑定现有素材索引，名称、分类与堆叠上限不变。图标标识 SHALL 仅由客户端展示使用，不随实例协议重复传输。

#### Scenario: 显式绑定与缺图
- **WHEN** 背包显示有图标、缺失图标或未找到图标的配置
- **THEN** 分别显示对应图集区域或占位，并始终保留配置名称提示
