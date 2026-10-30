# Proposal
## Why
真实 TCP 已接入，但 V2 进入世界后仍只读，玩家看不到其他连接的角色。需要由服务器独立推进移动、批量发布 AOI，并保持业务协议不承载连接会话字段。
## What Changes
- 20Hz 权威推进、10Hz AOI 合并发布；位置组包含坐标、目标及速度。
- 同角色接管、断线与切角离线；运行时元数据隔离旧任务，不进入业务 proto。
- 客户端原子缓存、点击预测、远端插值及缺口重同步，保留物品/GM 写禁用。
- 两进程真机链路、1000 TCP 连接基线与密集诊断。
## Capabilities
### New Capabilities
- `server-v2-movement`: 多人权威移动、观察范围同步和客户端呈现。
### Modified Capabilities
- `server-v2-account`: 用户追加默认 admin、demo1、demo2 账号，密码123，服务器模式限制admin。
- `server-v2-network`: 成功入角绑定控制权并支持接管与认证撤销。
- `server-v2-world-entry`: 入角后开放移动，角色进入具有控制边界。
- `server-v2-actor-protocol`: 独立运行时投递元数据，不改变原始业务协议移交。
## Impact
涉及 protov2 六个文件、Actor 运行时与三模块缓存、服务器独立调度、Session/World/UI；不迁移物品写业务，不新增退出或保存网络消息。
