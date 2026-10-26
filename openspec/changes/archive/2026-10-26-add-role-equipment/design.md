# Design

## Context

见 proposal.md。现有 BagPb/DataServerBag/RepoServerRole 已提供独立物品版本、不可变候选、单领域原子存档和失败重试；RolePreviewControl 可复用。当前 ActorView 固定加载1012，其他武器仅有图鉴首帧。用户确认拳刃两手可分别装备，盾牌与飞刀仅副手。

## Goals / Non-Goals

**Goals:** 复用现有九服务边界和拾取放置体验，装备状态在切角、消息及存档中完整一致。

**Non-Goals:** 不增加属性计算、战斗、职业、真实网络、双手占用副手限制。

## Decisions

- 装备列表放在现有背包物品领域，与 Items 共用 Revision 和 NextInstanceId。协议用独立 EquippedItemPb（稳定实例、道具、槽位枚举），BagPb 同时承载两侧。比新增装备文件/版本更容易保证原子转移，仍由 Slot 明确区分持有位置。旧存档缺失列表时为空。
- EquipItem 请求实例为正表示背包到装备，为0表示卸下目标槽；带角色、预期版本和可选卸下格。规则集中在纯服务端背包规则，静态道具规则复制分类枚举，不持有 Config；槽位判定复用代码工具。
- WorldService 在固定帧按不可变背包快照引用变化提交 SyncEquipmentAction；ECS 只保存装备道具编号，ActorView 经 relatedId/appearanceId 取资源。UI 和资源不写 ECS。
- 保留 TryGetSlot 作为默认推荐槽，增加 CanEquip 表达拳刃多槽。EquipItem 的目标槽始终显式传递。
- 装备表新增 appearanceId，保持 item.id/equipment.id 同值只是约定。导入工具按显式资源系列绑定七件装备，记录源路径、哈希、动作复用；副手拳刃复用对应拳刃原画并使用左手挂点，盾牌来源只有站立时保留真实素材并跟随身体，不伪造动画。
- 角色窗口复用预览和现有可滚动面板；跨窗口拿起状态由背包控件持有，角色槽位调用其公开交互入口，背包隐藏时不能保留悬挂物品。另提供槽位右键卸下。

## Risks / Trade-offs

- [部分素材缺独立动作] → 明确记录动作复用并覆盖全部当前动作；不宣称重建来源没有的帧。
- [状态并发或重复操作] → 共用版本拒绝旧操作，候选完整验证后提交，缓存一次替换。
- [旧存档] → 装备列表默认空，不自动发物品；保留现有延迟落地与共同转移提交机制。
- [小窗口] → 既有 ViewportPanelControl 滚动和边界验收继续覆盖。

## Migration Plan

升级后旧存档按空装备加载；首次物品变更写入新列表。配置和程序同包发布。回滚代码前需要卸下装备，避免旧程序忽略已穿戴物品。
