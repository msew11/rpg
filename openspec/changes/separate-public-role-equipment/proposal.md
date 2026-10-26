# Proposal

## Why

当前 `BagPb.equipment` 将其他玩家需要看到的穿戴信息放进仅本人可见的背包，世界装备表现也依赖私有背包快照。需要把装备作为独立公开领域，支持完整世界快照和单独装备推送，同时保留本人穿脱时背包与装备的原子更新。

## What Changes

- **BREAKING**：从 `BagPb` 删除装备字段并保留原字段号、名称；新增 `RoleWorldEquipmentPb`，在 `RoleWorldStatePb` 中与资料、状态、位置并列。
- 新增 `RoleEquipmentNotice`，只包含公开装备快照；装备不嵌入 `RoleWorldProfilePb`，拥有独立版本。
- **BREAKING**：以仅本人接收的 `RoleItemsChangedNotice` 替代 `BagChangedNotice`，完整携带并列的背包与装备快照，客户端跨两侧校验后一次接纳。
- 穿脱请求分别检查背包与装备版本；普通背包操作只更新背包版本，装备变化才更新装备版本。
- 保留服务器背包与装备的共同内存提交、现有同文件落地和实例编号规则；网络可见性拆分不要求拆分存档。
- 本轮设计及后续实现覆盖当前玩家闭环和公开载荷契约；远端玩家实体生命周期、玩家 AOI 订阅与多人服务端广播留给独立 change，不能宣称已实现多人可见。

## Capabilities

### New Capabilities

无，沿用现有装备、背包、会话与网络能力边界。

### Modified Capabilities

- `role-equipment`：装备成为独立公开快照，穿脱双版本校验并保持本人原子转移。
- `role-bag`：背包快照仅含背包物品，私有物品通知同时交付背包与装备。
- `client-session`：独立缓存装备，选角及本人物品变更原子接纳，世界转换不依赖私有背包。
- `mock-server`：公开世界快照增加装备，明确本人私有通知与观察者公开推送边界。
- `client-network`：增加装备协议文件与推送，替换私有物品通知并更新显式协议表。

## Impact

- 协议：`proto/bag.proto`、`role.proto`、`world.proto`，新增 `equipment.proto`；通过构建再生成 `src/Net/Message/`。
- 服务端：`MockServer` 的选角、物品提交与投影，`DataServerBag`/存档转换/规则增加装备版本，沿用 `<角色ID>.bag.json`。
- 客户端：`RoleCache`、`Session/Data`、`SessionService`、`WorldService`、装备与背包 UI、素材预热和消息注册。
- 验收：公开载荷不含背包或私有角色字段、版本隔离、原子替换、错误与过期消息、切角、旧存档和原有真实输入回归。
- 完成实现时同步 `docs/architecture.md` 与 `docs/roadmap.md`，再检查并归档；本提案不改变现有已实现能力的主 specs 或长期文档状态。
