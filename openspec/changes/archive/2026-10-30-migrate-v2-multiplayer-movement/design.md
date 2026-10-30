# Design
## Context
已完成 TCP 与延迟服务器实例，现有 Role 三表、World 对象缓存、ECS Action 和 Actor 顺序不可破坏。
## Goals / Non-Goals
Goals：服务器权威位置、多人可见、运行时统一隔离、真实网络与性能验收。
Non-Goals：战斗、寻路、传送、物品写、客户端保存协议和会话业务字段。
## Decisions
- 业务 proto 按用户批准的六文件结构扩展；移动目标与速度进入现有位置组，完整公开状态不重复包装。
- ActorRef Ask 添加 DataActorCall 参数，原消息不包装。邮箱在调用前校验，ModuleContext 暴露当前不可变调用元数据，跨 Actor 原样传播。元数据为身份值，不携带服务或委托；控制注册表属于运行时。C# 控制屏障在 Role/World 邮箱串行执行。
- 每角色控制版本、每连接认证版本仅在服务器内；接管和离线由运行时协调。成功业务以捕获身份交付，批量发布保存原路由元数据，不按最新连接重新绑定结果。
- RoleCache 组织独立不可变移动 DataCache，保留目标、速度、时间、版本、确认及投影重试。权威坐标同时 Commit 到角色表，Role Async Handler 向 World 提交完整公开投影。
- WorldCache 组织公开角色、观察者及块索引。更新时标记原块和新块观察者；每观察者保存上轮可见版本与范围，10Hz 构造差集。在线状态来自运行时控制注册。
- 后台单轮推进20Hz、发布10Hz，不堆积到期任务。保存与关闭包含调度屏障。
- 客户端 WorldSyncCache 统一原子接纳所有领域，公开角色只转换公开字段。WorldService 通过 Action 维护远端实体及插值；本机保持新意图并限流发包。
## Risks / Trade-offs
- 旧工作跨接管：进入邮箱前与输出边界校验控制版本，解绑先投递角色停止屏障再移除观察身份。
- 投影失败：角色权威不回滚，下一逻辑轮重试最新完整投影。
- 千连接压测：独立场景与临时数据，报告实际指标，常规套件只运行小规模功能场景。

## 默认账号追加
- 允许admin、demo1、demo2，密码原样123；规范账号仍Trim+ToLowerInvariant。服务器模式表单和Net发送入口限制admin/123，纯客户端初始demo1/123，demo2用于第二个远端玩家。
- 三个固定账号采用32位编号分区，demo1保留1003起始，admin和demo2分别加2^32与2×2^32；墓碑继续参与递增，分区耗尽拒绝，旧Server数据不迁移。
## 协议源文件
新增和修改协议位于protov2的role.proto、map.proto、world.proto、login.proto，以及actor/role.proto、actor/world.proto；生成代码由Grpc.Tools更新。业务消息无连接、认证或控制隔离字段，第一层公开组沿用RoleWorldXxxPb。
