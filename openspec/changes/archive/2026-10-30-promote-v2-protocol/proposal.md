# Proposal

## Why
现有业务已迁入 V2，继续保留旧协议及双分支增加维护成本。用户确认删除没有业务触发点的旧 Notice，将现有链路收敛为正式实现。

## What Changes
- **BREAKING** 删除旧协议、旧服务器及客户端旧协议分支，V2 成为唯一正式链路。
- 将 proto、ProtocolV2、ServerV2 归入正式 proto、Protocol、Server 目录与命名空间；存档根移除版本子目录并提升既有数据，保留正式字段编号。
- proto 消息体、枚举和 oneof 展开，每两个定义间空一行。
- 迁移仍有价值的验收，移除仅验证旧实现的测试和入口，更新文档及规格。

## Capabilities
### New Capabilities
- `game-protocol`: 唯一正式协议源及格式约定。
### Modified Capabilities
- `server-network`: 四字段信封使用正式协议源。
- `server-world-entry`: 移除旧服务器回归装配边界。
- `server-storage`: 正式存档根与原子提升。
- `client-network`、`client-session`: 唯一字节契约、世界帧与原子缓存。
- `mock-server`: 正式演示账号、快照、三表和共同落盘。
- `role-bag`、`role-equipment`: 最新状态执行及正式公开装备增量。
- `ground-item-drop`、`ground-item-pickup`: 移除预期版本和旧推送契约。

## Impact
proto、项目生成配置、服务器、网络、会话转换、UI、测试与文档。当前 V2 存档保持可读；不转换已删除旧服务器的存档格式，不新增玩法与未使用 Notice。
