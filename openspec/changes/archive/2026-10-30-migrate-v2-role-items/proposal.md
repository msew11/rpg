# Proposal

## Why

默认 V2 已能进入世界和多人移动，但背包、装备与 GM 仍只读。先迁移角色内物品闭环，按最新状态执行，复用现有交互。

## What Changes

- 新增 V2 GM、物品移动和主副手穿脱；本人原子接纳完整物品与装备，观察者接收公开装备。
- 不传预期版本、不新增物品版本或请求去重；保留权威校验和已有运行时隔离。
- Login → Role → World 编排，并同步选角摘要；物品操作与保存共用单实例串行入口。
- 地面操作由后续独立 change 完成，本次保持禁用。

## Capabilities

### New Capabilities
- `server-v2-items`: V2 最新状态物品操作、完整快照及多人装备同步。

### Modified Capabilities
- `server-v2-world-entry`: 已迁移物品入口开放，未迁移地面入口保持只读。

## Impact

涉及 protov2、ServerV2、Session、World、UI 和 C# 验收；不增加依赖、不迁移旧存档、不修改旧链路版本保护。
