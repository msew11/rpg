# Proposal

## Why
V2 角色物品已可写，接续已确认第四批第二项，完成地面物品闭环。

## What Changes
- 丢弃与销毁、走近后整份拾取、地图对象增量及现有 UI 开放。
- 保留 PickupItemResultNotice，仅 success；取消静默，背包改变不取消走近。
- 角色、摘要与地图使用专用单批次落盘记录，故障恢复完整旧状态。

## Capabilities
### New Capabilities
- `server-v2-ground-items`: 最窄地面转移与共同落盘。
### Modified Capabilities
- `server-v2-world-entry`: 开放地面入口。

## Impact
protov2、ServerV2、Session、World、UI、C# 回归与文档。不增加预期版本、去重、自动重试、通用事务协调器或转移准备协议。
