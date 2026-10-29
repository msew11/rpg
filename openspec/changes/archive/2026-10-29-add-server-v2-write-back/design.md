# V2 Actor 数据读写设计

## Context

动机见 proposal.md。ModuleActor 的 CreateContextAsync 是 INIT 加载入口，DispatchAsync 是 Flush 分发入口。现有目录缺少已启动分片枚举，连接队列缺少非关闭屏障，V2 传输尚未实现本地显式写回。

## Goals / Non-Goals

以 Actor 邮箱独占 IO 和状态生命周期；保留现有类型化数据消息。业务、认证、World 存储、旧存档迁移和跨 Actor 事务不在本次范围。

## Decisions

- Context 持有当前与已保存不可变快照，Commit/ReplaceSummaries 按内容计算差异。比 Handler 手工标记更不易遗漏，修改回原值自动取消待写。
- DataRole、DataItem、DataEquipment 放在 Role/Data 目录；DataRoleSnapshot 作为不可变仓库输入输出。角色字段沿用旧模型含义，性别用 V2 枚举，禁止引用旧 Server 类型。
- RepoAccountRoleSummary 和 RepoRole 负责异步读写，仓库归各自模块的 Storage 子目录，共用 Modules/Common/Storage 工具负责 JSON 与临时替换。多表先写原始字节回滚记录，单表直接替换；读取/保存前恢复，避免混合表。恢复记录只含允许的表名与原始内容，不接受路径。
- Flush 是固定类型请求，由 Actor 拦截，不注册为业务 Handler。保存期间 await 占用本 Actor 回合，避免另建后台写线程所需的版本确认。
- 宿主使用保存任务链串行化保存轮次，周期忙时跳过，显式保存不复用更早的周期轮次。连接队列插入无载荷屏障，显式调用先捕获屏障再枚举已启动分片；不提前停止连接。
- 周期由 MockServerService.Tick(delta) 累计现有环境间隔，V2 传输调用它。角色分片并行保存后再保存账号分片，汇总错误。
- 关闭冻结接纳并等待在途工作，然后执行最终 Flush，finally 停止模块；失败传播给释放调用方。未启动分片不因 Flush 而初始化。

## Risks / Trade-offs

- 保存占用本 Actor 回合 → 不阻塞其他分片，符合当前简洁框架目标。
- 周期保存可能丢失异常退出前尚未提交的修改 → 显式离开保存与正常关闭补写；不承诺每条业务响应都已落盘。
- 账号与角色分别提交 → 本次不提供角色创建/删除业务，未来业务另行确定跨分片一致性。
- 无业务校验 → 只处理反序列化及 IO 错误，事务元数据保留固定表名约束。

## Migration Plan

只接入显式 V2 构造，文件隔离于 server/v2；回滚本次提交即可恢复框架入口，旧服务端不受影响。
