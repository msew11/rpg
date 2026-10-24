## MODIFIED Requirements

### Requirement: 加载并索引道具表
ConfigService SHALL 在构造时通过现有 Godot 扫描链路加载唯一的 item.json，由标注 item.json 的 ItemCfgCache : MultiCfgCache<int, ItemCfg> 使用 item.schema.json 校验根数组，不使用 kind 或 items 包装。每行 SHALL 对应 ItemCfg，包含非空字符串 Name、正整数 Id、非负整数 Category（大类）和 SubCategory（小类）、正整数 MaxStack，数值字段均不超过 Int32 上限。Id SHALL 标注 CfgKey；服务 SHALL 持有 ItemCfgCache，通过 Item.Items 提供以标注主键为键的只读 Map；Map.Value 地图入口 SHALL 保持可用。

#### Scenario: 多行读取
- **WHEN** 配置目录包含合法地图和两条不同 ID 的道具记录
- **THEN** Item.Items 可按两个 ID 读取各自五字段，未知 ID 查询无匹配，Map.Value 仍可读取

#### Scenario: 空表
- **WHEN** item.json 根为 []
- **THEN** 加载成功且 Item.Items 为空，不自动补建道具

## ADDED Requirements

### Requirement: 指定测试装备配置
随程序的 item.json SHALL 包含以下七条道具，以 name 保存名称，maxStack 均为 1。category=1 表示武器、2 表示防具；其余大类仍待定。

| id | name | category | subCategory |
| --- | --- | --- | --- |
| 1012 | 测试长剑 | 1 | 2 |
| 1013 | 测试双手剑 | 1 | 4 |
| 1014 | 测试盾牌 | 2 | 105 |
| 1015 | 测试木杖 | 1 | 16 |
| 1016 | 测试枪 | 1 | 1 |
| 1017 | 测试拳刃 | 1 | 8 |
| 1018 | 测试飞刀 | 1 | 32 |

#### Scenario: 按编号读取测试装备
- **WHEN** ConfigService 加载随程序发布的道具表
- **THEN** Item.Items 可按上述七个 ID 读取对应名称、大类、小类和 maxStack=1

#### Scenario: 无效名称
- **WHEN** 道具记录的 name 缺失、为空字符串或不是字符串
- **THEN** 加载失败并报告对应配置文件的 Schema 错误
