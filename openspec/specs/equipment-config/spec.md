# equipment-config Specification

## Purpose
为后续角色装备功能建立独立装备配置、稳定槽位类型和代码分类映射，保证所有装备道具的配置引用在启动阶段完整可用，不将配置存在误认为已实现穿戴功能。

## Requirements

### Requirement: 独立装备表与道具引用
系统 SHALL 加载平铺的 equipment.json 数组及对应 Schema，装备记录以正整数 id 唯一索引。武器和防具道具的 relatedId SHALL 指向存在的装备 id；关联由装备缓存整表 Validate 重载在所有配置和索引加载完成后检查，不依赖文件顺序，编写时默认约定道具 id 与装备 id 同值，但不强制相等，允许多个道具引用同一装备。当前装备记录仅定义 id，不承载槽位或未确定的数值属性。

#### Scenario: 有效和共享引用
- **WHEN** 不同道具通过相同 relatedId 引用存在的装备记录
- **THEN** 启动成功，道具和装备可分别通过各自 id 查询

#### Scenario: 装备引用缺失
- **WHEN** 任意武器或防具记录的 relatedId 为零或不存在
- **THEN** 启动失败，错误包含 item.json、道具 id、relatedId 及 equipment.json

#### Scenario: 装备表非法
- **WHEN** 装备表缺失、重复 id、非正 id、null 行或出现未声明字段
- **THEN** 启动失败并包含配置文件诊断

#### Scenario: 空装备配置
- **WHEN** 道具表和装备表均为空
- **THEN** 启动成功且不自动补建记录

### Requirement: 枚举槽位与代码分类映射
系统 SHALL 以独立枚举定义主手、副手、头、面、身体、项链、手、戒指一、裤子、戒指二和鞋子，共十一种槽位。当前 SHALL 仅将武器大类与剑小类的组合映射至主手；其余组合无映射，装备表不声明槽位，不实现穿戴操作。

#### Scenario: 剑与其他类型
- **WHEN** 查询武器/剑、其他已定义武器或防具/盾牌的槽位
- **THEN** 仅武器/剑返回主手，其他组合返回未绑定
