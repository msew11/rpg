## Why

V2 已支持登录及摘要角色管理，需要独立验证从摘要进入世界、首次建档和重启恢复，同时保持真实服务器可用的协议边界。

## What Changes

- 新增 EnterWorld/EnterWorldRt，以及角色、私有背包和地图初始范围结构；共享结构与内部协议独立生成。
- Login 校验归属并先保存摘要，Role 受信任准备三表，World 返回裁剪九块；成功绑定连接角色并同步摘要。
- 三表全缺失允许尚未建档，部分缺失或损坏报错；首次保存失败保留候选供重试。
- 独立 V2 场景支持只读地图、人物、HUD、背包及装备，返回页面使用本地写回能力，不增加退出或保存网络协议。
- 保持默认旧游戏、邮箱生命周期和 Context → Cache → DataCache → Data 边界。

## Capabilities

### New Capabilities
- `server-v2-world-entry`: 进入世界、可信首次建档、初始展示、原子接纳和只读验收。

### Modified Capabilities
- `server-v2-account`: 独立客户端支持进入世界及摘要业务同步。
- `server-v2-storage`: 三表全缺失与部分缺失区分，首次待写基线。
- `server-v2-actor-protocol`: 共享结构导入与内部 ProtoRoot 调整。
- `server-v2-network`: 连接角色绑定及代次隔离。

## Impact

影响 protov2、ServerV2 业务模块和连接元数据、Session 缓存、只读世界 UI、独立验收场景与相关规格文档。无旧存档迁移，无默认入口切换，无网络保存或退出操作。
