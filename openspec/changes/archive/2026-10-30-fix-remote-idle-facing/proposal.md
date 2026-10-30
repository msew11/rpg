# Proposal

## Why

远程角色停止后被固定朝向覆盖，与本地角色保留最后移动方向的表现不一致。需要让停止和持续静止时的远端人物保持移动结束时的朝向。

## What Changes

- 远程样本保存最近有效朝向，目标清空或外推到达终点时不再强制朝下。
- 覆盖八方向停止、转向停止、静止样本淘汰和角色可见生命周期的回归场景。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `server-v2-movement`：明确远程角色停止后保留最近有效移动朝向及样本清理边界。

## Impact

涉及 `src/World/RemoteRoleBuffer.cs`、客户端 C# 回归测试和路线图。不增加协议字段或存档状态，首次可见且无移动信息时沿用现有默认面向。
