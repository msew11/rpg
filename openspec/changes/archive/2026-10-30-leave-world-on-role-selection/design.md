# Design

## Context

SessionService.LeaveRole 目前仅重置缓存和信封代次。代次变化有意保留服务端控制绑定，因此不能用 ResetSession 推断角色离线。现有 WorldCoordinator.DetachAsync 已提供断线/重认证时的停止移动、控制撤销和 World 成员移除。

## Goals / Non-Goals

**Goals:** 页面返回通过显式请求结束世界参与，复用既有控制清理及有序 AOI；账号仍可管理角色和重新进入。

**Non-Goals:** 不新增保存协议、不销毁共享 Actor，不改变断线和服务器关闭策略。

## Decisions

- 在 world.proto 增加空 LeaveWorld 和 LeaveWorldRt，显式注册消息方向。由 ServerRuntime 校验认证后调用现有 DetachAsync；身份来自连接，不向 Actor 传连接。离开为运行时控制操作，与重同步及断线使用相同边界。
- 客户端先隔离旧代次和在途消息，再发送 LeaveWorld，随后清空角色缓存。保持同连接串行请求，重复离开幂等；后续重新进入按 EnterWorld 流程建立新控制及首帧。只对实际角色或待进入角色发送离开，空列表刷新不发送。
- 取消旧控制先于角色/地图邮箱排空，可拒绝旧移动调用；DetachAsync 只处理该连接仍持有的绑定，被接管连接不能让新控制者下线。
- 现有规格和架构规定“返回选角保留绑定/无退出协议”，按本次用户明确目标更新该边界。保存仍归原本地屏障及服务端周期/关闭流程。

## Risks / Trade-offs

- 异步离开需要网络往返及 AOI 节奏 → 下一次正常 10Hz 发布移除，网络错误由既有错误路径反馈。
- 与快速切角相邻的请求 → 同连接串行处理，EnterWorld 本身也会替换旧绑定；回归覆盖重新进入。
- 新消息对旧服务器不可用 → 客户端和服务器同步部署，无存档迁移。
