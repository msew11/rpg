# Proposal

## Why

用户希望角色默认手持测试长剑 1012，并跟随人物八方向。当前只有背包图标和武器首帧图鉴，没有角色持握表现。

## What Changes

- 核对 1012 原始右手长剑资源，导入男女动作帧、连接点与遮挡数据。
- 角色默认增加独立武器层，共用身体动作时钟、朝向和镜像；站立、跑步及转向保持握点同步。
- 增加资源来源、实际节点回归验收及八方向预览。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-actor-presentation`: 默认持握武器 1012 的分层八方向表现。

## Impact

涉及角色资源导入、AnimationService、ActorView、外观测试与架构/路线图；只增加默认外观，不增加背包装备、属性或战斗规则，不修改协议与存档。
