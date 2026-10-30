# Design

## Context
目前客户端存在旧协议与 V2 双转换、双监听和测试装配。正式入口已经使用 TCP V2；旧服务器只能由测试显式装配。

## Goals / Non-Goals
收敛唯一协议与服务器，保持已迁移业务、现有 V2 存档和真实 UI 行为。不新增没有触发点的资料、生命魔力、经验推送。

## Decisions
- 正式源为 proto/，客户端生成到 src/Protocol/Generated，命名空间 Rpg.Protocol、package rpg；内部消息仍按 actor/ 分类，命名空间 Rpg.Server.ActorProtocol、package rpg.actor。
- 原 ServerV2 移到 Server，删除旧 Server 和旧字节传输；生产 TCP 与进程内测试使用相同协议、模块和缓存。
- 去掉版本选择开关、旧协议转换和监听。保留客户端独立不可变数据模型；进入、世界帧与物品组合仍完整验证后接纳。
- 存档根收敛为 user://server，自动把已有 server/v2 数据迁到正式根目录；线上协议类型名切换，客户端与服务器一起更新。
- 原 V2 服务器测试作为正式回归。旧服务器专属测试随实现移除；可见功能仍通过正式业务测试、真实鼠标及双进程 TCP 验证。
- proto 所有非空体展开到独立行，空消息也使用两行大括号；字段缩进两空格，定义之间恰好空一行。

## Risks / Trade-offs
旧协议和旧测试装配不再兼容。重点检查缓存接纳、UI 按钮、测试建档及同名生成类残留；完整构建和功能检查验证唯一链路。
