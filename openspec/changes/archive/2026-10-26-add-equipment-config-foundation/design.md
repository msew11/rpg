# Design

## Context

现有 CfgCache 先做 Schema 再解析，MultiCfgCache 已提供逐行 Validate 钩子。ConfigService 在加载循环后内联检查初始地图。ItemCfg 的分类均为 int，七条正式记录分别为六种武器和盾牌；部分测试使用 0/0 表示任意堆叠道具。

## Goals / Non-Goals

**Goals:** 配置枚举、分类索引和表引用构成启动期间完成的完整契约，后续功能不必重新扫描表。具体目标见 proposal 和 specs。

**Non-Goals:** 穿戴协议、装备实例、属性运算、运行时外观接入、UI，以及未经确认的装备属性。

## Decisions

1. 在 Const 定义 ItemCategory（Weapon=1、Armor=2）、ItemSubCategory（1/2/4/8/16/32/105）和十一种 EquipmentSlot，使用显式数字而非 Flags。保持已有 JSON 数字。ItemCfg 的分类使用 required 非空枚举；ItemCfgCache 通过 Enum.IsDefined 和组合校验拒绝未定义值与错配，不为测试引入 0 占位分类。未来分类按独立需求增加。
2. 独立 EquipmentSlotUtil 只绑定 Weapon/Sword 到 MainHand；其余组合 TryGetSlot 返回 false，不能从任意武器、名称或装备表推定可穿槽位。
3. ItemCfg 增加 required RelatedId（JSON relatedId），EquipmentCfg 目前仅有 Id。编写约定优先让装备 id 与道具 id 同值，正式样例使用 1012—1018；不强制相等，每件道具仍通过 relatedId 显式关联。武器和防具都属于装备，均由 EquipmentCfgCache.Validate 检查，允许共享引用，不要求装备记录一定被引用。不虚构攻击力等字段。
4. CfgCache.Load 完成 Read 后调用 protected virtual BuildIndexes；ItemCfgCache 内部维护 Dictionary<ItemCategory,List<ItemCfg>> 和 Dictionary<(ItemCategory,ItemSubCategory),List<ItemCfg>>。公开两种查询返回列表副本，既满足 List 值类型又不暴露可修改缓存；释放时清理索引。
5. 保留已有 Validate(TCfg) 逐行检查，CfgCache 新增 protected virtual Validate(ConfigService) 整表关联重载，由 internal ValidateReferences 包装生命周期和文件诊断。ConfigService 在所有 Load 返回后统一遍历入口；EquipmentCfgCache 直接重载检查装备引用，BasicCfgCache 直接重载检查初始地图，不增加策略类或目录。
6. 更新所有隔离配置夹具和堆叠验收数据，使用合法武器/剑分类及存在的关联，而不改变原有堆叠验收目的。离线配置页同步示例与 Schema，加入装备表入口。

## Risks / Trade-offs

- 分类收紧使旧任意整数配置失效 → 启动错误包含文件、道具及分类；同步迁移仓库夹具。
- List 可修改 → 查询返回副本，内部索引不暴露；缓存释放后拒绝再次查询。
- 装备表暂无已确认属性 → 仅建立主键和引用结构，后续需求再扩展。

## Migration Plan

一起提交代码、Schema、正式数据和测试夹具；不改用户存档。回滚可直接回滚本次本地提交。
