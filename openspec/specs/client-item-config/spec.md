# client-item-config Specification

## Purpose

提供启动时加载和校验的道具配置表，按唯一道具编号读取包含名称和四个数值字段的类型化记录，为后续道具功能建立稳定的数据入口，并保持配置只读与服务生命周期约束。

## Requirements

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

### Requirement: 无效道具配置阻止启动
系统 SHALL 拒绝缺失道具表、配置子目录、重复 ID、错误文件名、损坏 JSON 及不符合 Schema 的字段；文件内容错误 SHALL 包含文件路径，重复 ID 错误 SHALL 指明编号。

#### Scenario: 记录无效
- **WHEN** 记录缺字段、字段类型错误、编号或堆叠上限非正、分类为负、包含未知字段或超出整数范围
- **THEN** 构造失败，错误指向对应配置文件

#### Scenario: 编号重复
- **WHEN** 两行具有相同 id
- **THEN** 加载失败并指出重复编号与文件

#### Scenario: 文件契约错误
- **WHEN** 道具表缺失、位于子目录，或道具配置文件未命名为 ItemCfgCache 标注的 item.json
- **THEN** 加载失败并报告对应原因

### Requirement: 道具配置生命周期
道具记录 SHALL 使用初始化后不可写的属性，Map SHALL 不允许调用方增删改，ConfigService 释放后 SHALL 拒绝从 Item.Items 读取，先前取得的 ItemCfgCache 引用也 SHALL 拒绝读取。

#### Scenario: 只读与释放
- **WHEN** 调用方尝试修改映射或释放后访问服务的 Item.Items
- **THEN** 操作被拒绝

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

### Requirement: 道具图标配置
ItemCfg SHALL 支持可选字符串 IconAssetId，缺失时为空；非字符串 SHALL 被 Schema 拒绝。正式七件测试装备 SHALL 显式绑定现有素材索引，名称、分类与堆叠上限不变。图标标识 SHALL 仅由客户端展示使用，不随实例协议重复传输。

#### Scenario: 显式绑定与缺图
- **WHEN** 背包显示有图标、缺失图标或未找到图标的配置
- **THEN** 分别显示对应图集区域或占位，并始终保留配置名称提示

### Requirement: 独立地面素材配置
ItemCfg SHALL 支持可选字符串 GroundAssetId，缺失为空，非字符串被 Schema 拒绝。地面表现 SHALL 仅使用此键解析 item/item 图集区域，缺图使用占位；背包继续使用 IconAssetId。七件测试装备 SHALL 按来源记录 icon_small_code 显式绑定地面素材，不通过道具 ID 或背包图标名称推断。资源键只用于客户端，不进入实例协议及服务端存档。

#### Scenario: 地面与背包独立
- **WHEN** 同一道具分别显示在地面和背包
- **THEN** 使用各自配置的素材，缺失地面键时显示占位，不影响背包图标

#### Scenario: 类型校验
- **WHEN** groundAssetId 为非字符串
- **THEN** 配置加载失败并报告 Schema 错误

### Requirement: 道具分类列表索引
道具缓存 SHALL 在整表读取成功后构建以 Category 为键和以 (Category, SubCategory) 为双键的索引，值均为 List<ItemCfg>，保留同组全部记录及配置顺序。查询 SHALL 返回列表副本，未命中返回空 List；修改返回列表不得污染缓存，释放后查询 SHALL 抛异常。

#### Scenario: 同分类多条道具
- **WHEN** 多条剑和一条其他武器加载完成
- **THEN** 大类查询返回全部武器，大小类查询返回全部剑，不因重复分类覆盖道具

#### Scenario: 查询隔离和生命周期
- **WHEN** 清空查询结果、查询未存在分组或释放已取得的缓存
- **THEN** 后续查询结果不变、未命中得到空列表，释放后两种查询均被拒绝
