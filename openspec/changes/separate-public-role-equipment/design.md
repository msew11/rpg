# Design

## Context

动机见 proposal.md。当前 `BagPb` 同时包含背包和装备，`RoleCache` 在转换 `BagPbData` 时校验两侧实例唯一性，`WorldService.SyncEquipment` 也从背包读取主副手。`DataServerBag` 把两侧内容、版本和下一个实例编号共同保存到 `<roleId>.bag.json`。

当前 MockServer 仅维护一个当前角色会话，地图 AOI 只覆盖怪物和地面物品，没有远端玩家进入/离开或多连接广播。因此本设计明确公开装备协议和接收规则，本次后续实现不扩建多人网络体系。

## Goals / Non-Goals

**Goals:**
- 无须私有背包或私有 Role 数据即可构造、解码和使用装备快照。
- 完整世界状态含装备；换装备可独立推送，不连带更新名称、等级、生命魔力或位置。
- 本人背包与装备继续原子接纳，实例只在一个位置存在；版本过滤和切角隔离仍有效。

**Non-Goals:**
- 不新增装备属性、强化、隐藏装备、查看他人详情或新穿脱规则。
- 不新增远端玩家缓存、实体生命周期、玩家 AOI 或真实服务端广播；公开载荷契约为这些后续功能提供输入。
- 不按网络公开/私有边界拆分持久化文件。

## Decisions

### 1. 独立装备领域，与 Profile 并列

采用 `RoleWorldEquipmentPb`；完整世界状态中的装备与 `profile/status/position` 并列。`RoleWorldProfilePb` 保持身份及基础外观资料，装备单独版本化。

把装备嵌入 Profile 虽能复用资料推送，但会让穿脱抬高资料版本，或在 Profile 内引入第二个独立版本；独立领域更符合现有世界协议的拆分方式。只放在独立推送中也不可取，否则新观察者拿到完整世界状态仍无法还原当前装备。

以下是目标协议片段，既有字段保留原编号，未列出的既有消息保持原含义：

```proto
// bag.proto：仅本人可见的背包物品，不含穿戴列表。
message BagPb {
  // 本人物品提交序号，穿脱同样递增。
  int64 revision = 1;
  // 仅处于背包格子的实例。
  repeated ItemPb items = 2;
  reserved 3;
  reserved "equipment";
}

// equipment.proto：可公开的完整穿戴快照。
message RoleWorldEquipmentPb {
  // 穿戴者身份，不是接收者身份。
  int64 role_id = 1;
  // 仅装备内容变化才递增。
  int64 revision = 2;
  // 只记录已占用槽位，空列表表示全部卸下。
  repeated EquippedItemPb items = 3;
}

// role.proto：完整公开状态，不依赖私有 RolePb 或 BagPb。
message RoleWorldStatePb {
  // 基础公开资料。
  RoleWorldProfilePb profile = 1;
  // 所属地图与位置。
  RoleWorldPositionPb position = 2;
  // 公开生命魔力状态。
  RoleWorldStatusPb status = 3;
  // 独立公开的当前装备。
  RoleWorldEquipmentPb equipment = 4;
}

// world.proto：可发给穿戴者的可见范围观察者。
message RoleEquipmentNotice {
  // 最新完整装备，缺失字段是非法消息，不表示卸下。
  RoleWorldEquipmentPb equipment = 1;
}

// bag.proto：只发给本人的完整物品提交，替代 BagChangedNotice。
message RoleItemsChangedNotice {
  // 物品所属角色，须与装备快照身份一致。
  int64 role_id = 1;
  // 同一次提交的完整私有背包。
  BagPb bag = 2;
  // 同一次提交的完整公开装备。
  RoleWorldEquipmentPb equipment = 3;
}
```

`EquipmentSlotPb` 与 `EquippedItemPb` 从 bag.proto 移到 equipment.proto，保留完整名称、槽位数值和字段号。现有装备记录的 `instance_id/item_id/slot` 作为当前公开字段；实例编号用于识别同配置的替换，并不携带背包位置、数量或私有角色信息。未来如新增私有随机属性，需要另行设计私有结构，不自动加入公开记录。

`SelectRoleRt` 保留现有字段：`state.equipment` 返回初始装备，`bag` 返回初始背包，`role` 返回私有玩法领域；不重复增加选角响应顶层装备字段。空装备也必须显式返回 `role_id` 与有效版本，不能用缺失装备字段表示空手。

### 2. 接收者决定投递渠道，快照归属只表达穿戴者

| 消息 | 接收者 | 状态内容 |
| --- | --- | --- |
| `SelectRoleRt` | 选角本人 | 公开世界状态 + 私有 Role + 私有背包 |
| `RoleStateNotice` | 有可见权限的观察者 | 公开资料、位置、生命魔力、装备 |
| `RoleEquipmentNotice` | 有可见权限的其他观察者 | 穿戴者身份、装备版本、已穿实例 |
| `RoleItemsChangedNotice` | 物品所属本人 | 背包与装备的原子组合 |
| `EquipItemRt` | 请求本人 | 成功/失败结果，不承载第二份可写状态 |

成功穿脱提交内存后，本人收到 `RoleItemsChangedNotice` 与结果响应；观察者按自己的连接和信封代次收到 `RoleEquipmentNotice`，不能转发本人的私有通知。观察者范围由未来的玩家可见性系统决定，不能按全服广播实现。当前 MockServer 实现本人渠道；公开投影与字节编解码契约必须可独立验收，不伪造已存在的多人接收者。

### 3. 分离公开装备版本，保留本人物品提交顺序

保留现有 `DataServerBag.Revision` 和 `BagPb.revision` 作为本人物品提交序号：每次真实物品变更递增，包括穿脱，避免另加一个事务序号。增加独立 `EquipmentRevision`，只在装备列表实际变化时递增。名称、等级、位置和生命魔力版本均不因穿脱变化。

| 操作 | 背包/本人物品 revision | equipment revision | 本人通知 | 观察者装备通知 |
| --- | --- | --- | --- | --- |
| GM 发放、背包移动、丢弃、拾取 | +1 | 保持 | 完整物品快照 | 无 |
| 穿戴、替换、卸下 | +1 | +1 | 完整物品快照 | 有 |
| 移动角色或修改基础资料 | 保持 | 保持 | 沿用对应领域通知 | 无 |
| 失败、取消、无操作 | 保持 | 保持 | 无成功变更快照 | 无 |

`EquipItem.expected_revision = 4` 改名为 `expected_bag_revision`，字段号不变；增加 `expected_equipment_revision = 6`，保留 `target_bag_slot = 5`。请求其余字段不变。两个版本任一过期即整份拒绝；MoveItem、DropItem、PickupItem 仍只检查背包版本。拿起装备时也保存两个版本。

本人所有物品变更统一使用 `RoleItemsChangedNotice`，包含未变化的装备快照，替代原 `BagChangedNotice`。仅有十一槽位，重复发送装备的小额成本换来单一原子接纳路径，避免为背包单独通知与装备通知另建跨消息配对缓冲。

### 4. 本人原子缓存与公开装备转换分别处理

- 新增不可变 `RoleWorldEquipmentPbData`，提供不依赖背包容量或私有 Role 的 `FromPb/ToPb`，检查角色 ID、版本、槽位、实例唯一性和配置适配。
- `BagPbData` 只保存背包；`RoleCache` 独立持有装备，通过本地角色组合快照和 Session 只读入口提供给消费者。不新增 BagCache、DataCache 或服务业务订阅。
- 选角先转换所有领域，再检查身份一致和背包/装备跨列表实例唯一性，全部成功才发布任何缓存。
- 本人物品通知以 `bag.revision` 过滤整份提交，非较新版本忽略；较新通知的装备版本不得回退，同装备版本必须对应相同完整装备内容。两侧校验、跨列表唯一性检查及角色组合快照构造全部完成后一次替换。
- 当前玩家的装备权威更新只从选角或私有物品通知接纳。`RoleEquipmentNotice` 供其他角色的公开状态消费；属于本人的独立装备推送不更新本地装备。完整 `RoleStateNotice` 中本人的装备同样不能绕过物品原子路径，其他可见领域仍按各自版本更新。服务端任何改变本人装备的入口都必须交付私有组合通知。
- 未来远端玩家可仅依据角色 ID 与装备版本接纳公开装备，不需要背包或 RolePb。首次进入可见范围以完整世界快照初始化，后续装备全量覆盖；离开后须由未来玩家可见性系统控制晚到消息，本 change 不创建幽灵角色。
- `WorldService.SyncEquipment` 改为追踪装备快照及其版本，继续提交 `SyncEquipmentAction`。普通背包操作不产生装备同步动作。角色窗口、素材预热及跨窗口穿脱从独立装备入口读取，保留原交互与外观。

### 5. 网络拆分沿用原存档事务

`DataServerBag` 继续作为服务端物品聚合，保留 Equipment、Items、NextInstanceId 和现有 `.bag.json`；公开快照在输出阶段显式投影，不序列化整个私有聚合。新增可缺省的 EquipmentRevision，旧存档加载时取现有 Revision 作为初始装备版本，已有装备内容完整保留；新角色两版本均为 0。该缺省值须在存档转换边界处理，运行态不保留“版本缺失”的状态。

仓库相等比较、哈希、校验及读写转换纳入装备版本；读取旧档不立即写盘，下次原有 bag 待写提交时保存新字段。装备和背包继续一起候选校验、提交内存、标记待写并共同落地，保留落地失败重试及地图转移恢复语义。

## Risks / Trade-offs

- 新旧客户端协议不互通 → 当前客户端和进程内 MockServer 同包升级；保留删除字段号和名称，禁止用旧 `BagPb` 字段恢复装备，也不双发旧私有通知。
- 本人公私消息交错可能短暂重复实例 → 本人只接纳私有组合物品提交，公开装备消息不替换本人状态；非法组合保留原缓存并报告错误，使用现有返回选角再选角流程重新取得完整状态。
- 每次普通背包变更重复带十一槽装备 → 当前范围接受该成本；以后若规模确有必要再引入配对序号和分片协议。
- 公开协议具备独立载荷不等于多人系统完成 → 验收分别报告当前玩家闭环和公开消息契约，远端实体及广播保持待实现。
- 同一次提交中一侧非法 → 先构造候选并完整校验，缓存阶段失败时不通知普通监听，禁止部分替换。

## Migration Plan

1. 新增 equipment.proto，移出既有装备数据、枚举及穿脱请求响应，保留其完整消息名和字段号；bag.proto 引用 equipment.proto 定义私有组合通知，role.proto 直接引用两文件，world.proto 直接引用 role.proto 与 equipment.proto，避免循环引用。
2. 更新 bag/role/world 及显式协议表，移除 `BagChangedNotice` 注册，通过 `dotnet build` 再生成协议代码，不手改生成文件。
3. 增加并恢复装备版本，调整服务器物品提交及公开投影，再迁移缓存、World、UI 和素材预热消费者。
4. 迁移既有测试到新消息，补充可见性边界、双版本、本人原子接纳及旧档回归，运行项目检查。
5. 更新架构与路线图的当前能力说明并同步 delta specs，完成后归档 change。保留清晰中文约定式本地提交，不推送。

回滚采用整次变更回退，恢复配套协议生成文件和消费者。保留原存档内容与实例编号；新增装备版本字段采用向前兼容的额外 JSON 字段，不改写既有物品位置以配合协议迁移。
