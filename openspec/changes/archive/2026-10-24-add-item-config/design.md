## Context

ConfigService 已用 Godot 递归读取 JSON，以 kind 分派并校验地图。新增表格与单对象配置共享这条加载链路。

## Goals / Non-Goals

**Goals:** 加载 item.json，以只读字典提供唯一 ID 到 ItemCfg 的映射，保持地图读取和服务生命周期。

**Non-Goals:** 不实现背包、穿戴、协议、热重载，不把图鉴清单转成正式玩法配置。

## Decisions

- 文件采用 `{ "kind": "item", "items": [] }`，保持 kind 分派；相比裸数组，扫描时可统一确定类型。每行包含 id、category、subCategory、maxStack。
- ItemCfg 使用 init 属性，字段均为 int。id 和 maxStack 为正整数，分类为非负整数，Schema 限制 Int32 范围。不新增分类枚举，语义待确认。
- ConfigService 私有持有 IReadOnlyDictionary<int, ItemCfg>，公开 ItemCfgs；加载完成后用 ReadOnlyDictionary 包装，不暴露可写字典。相比 List，按 ID 查询直接且重复 ID 可立即诊断。
- 地图与道具表均为启动必需；允许空道具表以免编造正式数据。加载先在局部校验完整，再提交到服务字段。未知 kind、文件名错误、重复表、重复 ID、Schema 错误均带文件诊断。
- 使用 Godot headless 执行加载验收；覆盖多行读取、空表、缺表、重复/无效记录、重复文件、释放访问与地图共存。

## Risks / Trade-offs

- 尚无正式分类和堆叠数值 → 正式表保持空，验收数据仅存在测试中。
- 图鉴的重复 ID 不符合正式索引 → 两类数据继续独立管理。
