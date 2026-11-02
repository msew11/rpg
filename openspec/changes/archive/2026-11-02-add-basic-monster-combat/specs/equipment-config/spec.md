## MODIFIED Requirements

### Requirement: 独立装备表与道具引用
系统 SHALL 加载平铺的 equipment.json 数组及对应 Schema，装备记录以正整数 id 唯一索引。武器和防具道具的 relatedId SHALL 指向存在的装备 id；关联由装备缓存整表 Validate 重载在所有配置和索引加载完成后检查，不依赖文件顺序，编写时默认约定道具 id 与装备 id 同值，但不强制相等，允许多个道具引用同一装备。装备记录 SHALL 通过正整数 appearanceId 显式关联项目内持握资源，不承载槽位；每个武器 SHALL 配置整数基础点伤上下限、正攻击范围和正每秒攻击次数，法杖基础范围为近战范围。

#### Scenario: 有效和共享引用
- **WHEN** 不同道具通过相同 relatedId 引用存在的装备记录
- **THEN** 启动成功，道具和装备可分别通过各自 id 查询

#### Scenario: 装备引用缺失
- **WHEN** 任意武器或防具记录的 relatedId 为零或不存在
- **THEN** 启动失败，错误包含 item.json、道具 id、relatedId 及 equipment.json

#### Scenario: 装备表非法
- **WHEN** 装备表缺失、重复 id、非正 id、无效外观关联、null 行或出现未声明字段
- **THEN** 启动失败并包含配置文件诊断

#### Scenario: 空装备配置
- **WHEN** 道具表和装备表均为空
- **THEN** 启动成功且不自动补建记录
