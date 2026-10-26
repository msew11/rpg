## MODIFIED Requirements

### Requirement: 加载并索引道具表
ConfigService SHALL 在构造时通过现有 Godot 扫描链路加载唯一的 item.json，由标注 item.json 的 ItemCfgCache : MultiCfgCache<int, ItemCfg> 使用 item.schema.json 校验根数组，不使用 kind 或 items 包装。每行 SHALL 对应 ItemCfg，包含非空字符串 Name、正整数 Id、非空枚举 Category 和 SubCategory、非负整数 RelatedId 及正整数 MaxStack，整数不超过 Int32 上限。分类 JSON 保留数字，缺失、null、未定义枚举值或不匹配的大小类 SHALL 阻止启动。Id SHALL 标注 CfgKey；Item.Items SHALL 提供按主键的只读映射，地图查询保持可用。

#### Scenario: 多行读取
- **WHEN** 配置目录包含合法地图、装备和两条不同 ID 的道具记录
- **THEN** Item.Items 可按两个 ID 读取类型化分类与关联 ID，未知 ID 查询无匹配，地图仍可读取

#### Scenario: 空表
- **WHEN** item.json 根为 []
- **THEN** 加载成功且 Item.Items 为空，不自动补建道具

#### Scenario: 未定义枚举与不匹配分类
- **WHEN** 分类为未定义的非负整数、null 或缺失，或武器大类搭配盾牌小类
- **THEN** 启动失败，错误包含 item.json 和分类原因

## ADDED Requirements

### Requirement: 道具分类列表索引
道具缓存 SHALL 在整表读取成功后构建以 Category 为键和以 (Category, SubCategory) 为双键的索引，值均为 List<ItemCfg>，保留同组全部记录及配置顺序。查询 SHALL 返回列表副本，未命中返回空 List；修改返回列表不得污染缓存，释放后查询 SHALL 抛异常。

#### Scenario: 同分类多条道具
- **WHEN** 多条剑和一条其他武器加载完成
- **THEN** 大类查询返回全部武器，大小类查询返回全部剑，不因重复分类覆盖道具

#### Scenario: 查询隔离和生命周期
- **WHEN** 清空查询结果、查询未存在分组或释放已取得的缓存
- **THEN** 后续查询结果不变、未命中得到空列表，释放后两种查询均被拒绝
