# Design

## Context

现有 Handler 是纯内存操作，但 RepoEntityCache 和 MockServer 集中了所有领域。现行四表和地图共同事务必须保留。

## Goals / Non-Goals

采用每 Actor 独立异步串行邮箱，明确缓存归属。保持当前单客户端协议，不增加真实网络或游戏规则。

## Decisions

- BaseActor 使用 Channel 与 Task，INIT 首次消息加载后进入 RUN，停止排空，失败完成所有 Ask。
- LoginActor 拥有 ServerLoginCache；RoleActor 按 RoleId 拥有 ServerRoleCache；WorldActor 按 MapId 拥有 ServerWorldCache。ServerRequestContext 仅提供 Handler 本次纯内存处理入口，替代旧全局 RepoEntityCache。
- RoleActor 读取自己的三张实体表后 Ask WorldActor。地图相关处理把角色工作副本发往 WorldActor 串行执行，返回结果后 RoleActor 发布自己的状态，避免直接共享可写角色状态。摘要以消息同步到 LoginActor。
- MockServer 只承担客户端会话、消息编码、分片目录和周期/共同提交协调。同步客户端边界等待 Actor Ask，输出继续在调用线程交给 Godot，避免线程访问 UI。
- 保留当前文件事务格式，先排空业务，再在分片邮箱中执行准备写和确认，失败保持 dirty。正常传输关闭排空并停止全部 Actor。

## Risks / Trade-offs

- 同步客户端边界仍会等待 IO → 后台 Actor 执行与 Godot UI 线程隔离，但本次不变更传输接口。
- 跨 Actor Ask 循环导致死锁 → 调用只向 Role→World 和外层→Login/Role/World，摘要在角色回合结束后同步，World 不反向等待 Role。
- 角色工作副本遗漏瞬态状态 → 回归延迟移动、拾取取消、AOI 序号、版本校验及重置。
