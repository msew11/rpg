# client-item-config Specification

## Purpose

提供启动时加载和校验的道具配置表，按唯一道具编号读取四字段类型化记录，为后续道具功能建立稳定的数据入口，并保持配置只读与服务生命周期约束。

## Requirements

### Requirement: 加载并索引道具表
ConfigService SHALL 在构造时通过现有 Godot 扫描链路加载唯一的 item.json，使用 item.schema.json 校验 kind=item 与 items 数组。每行 SHALL 对应 ItemCfg，包含正整数 Id、非负整数 Category（大类）和 SubCategory（小类）、正整数 MaxStack，均不超过 Int32 上限。服务 SHALL 通过 ItemCfgs 提供以 Id 为键的只读 Map；地图入口 SHALL 保持可用。

#### Scenario: 多行读取
- **WHEN** 配置目录包含合法地图和两条不同 ID 的道具记录
- **THEN** ItemCfgs 可按两个 ID 读取各自四字段，未知 ID 查询无匹配，MapConfig 仍可读取

#### Scenario: 空表
- **WHEN** item.json 的 items 是空数组
- **THEN** 加载成功且 ItemCfgs 为空，不自动补建道具

### Requirement: 无效道具配置阻止启动
系统 SHALL 拒绝缺失道具表、重复表、重复 ID、错误文件名、损坏 JSON 及不符合 Schema 的字段；文件内容错误 SHALL 包含文件路径，重复 ID 错误 SHALL 指明编号。

#### Scenario: 记录无效
- **WHEN** 记录缺字段、字段类型错误、编号或堆叠上限非正、分类为负、包含未知字段或超出整数范围
- **THEN** 构造失败，错误指向对应配置文件

#### Scenario: 编号重复
- **WHEN** 两行具有相同 id
- **THEN** 加载失败并指出重复编号与文件

#### Scenario: 文件契约错误
- **WHEN** 道具表缺失、存在两份，或 kind=item 的文件未命名为 item.json
- **THEN** 加载失败并报告对应原因

### Requirement: 道具配置生命周期
道具记录 SHALL 使用初始化后不可写的属性，Map SHALL 不允许调用方增删改，ConfigService 释放后 SHALL 拒绝从 ItemCfgs 读取。

#### Scenario: 只读与释放
- **WHEN** 调用方尝试修改映射或释放后访问服务的 ItemCfgs
- **THEN** 操作被拒绝
