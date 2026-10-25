# Design

## Context

现有 RoleWorldProfilePb 混合外观、生命魔力与私有玩法字段；RoleCache 只独立保存资料和位置。背包已经通过 SelectRoleRt.bag 及独立通知管理。

## Goals / Non-Goals

**Goals:** 世界协议只携带其他玩家可见的数据；私有 RolePb 与 BagPb 并列；保留本地 UI 的组合读取能力。

**Non-Goals:** 不引入多人广播或更改 JSON 存档领域；不拆分生命、魔力的各自更新频率。

## Decisions

- RoleWorldProfilePb 保留 ID、名字、等级、性别、头型；RoleWorldStatusPb 承载 ID、生命、上限、魔力、上限和版本；RoleWorldStatePb 组合资料、状态、位置。
- RolePb 承载 ID、经验、升级经验、背包扩容和版本，放入 SelectRoleRt.role，与 BagPb 并列；RoleChangedNotice 只发给当前玩家。版本沿用服务端递增的角色版本，但客户端各领域独立过滤。
- RoleCache 先验证选角四部分及背包再一次提交。世界状态更新不读取或重写私有 Role；RoleChangedNotice 更新私有数据并重新校验背包容量。
- 本地组合数据继续服务现有 HUD，但其 ToPb 只输出可见世界字段。服务端公开的完整世界推送若同时变更私有字段，应另发 RoleChangedNotice。

## Risks / Trade-offs

协议不兼容旧字节；生成代码及测试须同步更新。局部推送必须避免隐式改变其他领域，否则客户端无法得知变化。
