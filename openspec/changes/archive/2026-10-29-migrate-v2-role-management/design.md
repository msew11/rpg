# Design

## Context

参见 proposal.md。现有 LoginActor INIT 读取不可变摘要，Context 引用 LoginCache，由 DataCacheRoleSummary 跟踪保存基线，宿主负责周期和关闭写回。默认客户端仍走旧协议。

## Goals / Non-Goals

只在 Login 模块实现账号摘要闭环，不改变 Runtime、连接、信封和保存顺序。不实现进入世界、正式认证、跨 Actor 事务或旧存档迁移。

## Decisions

- Login/CreateRole/DeleteRole 协议直接经路由进入 LoginActor，成功返回完整可见列表，统一 ErrorRt 表达失败。账号只取服务端会话。
- 摘要管理归 LoginCache.Summaries（DataCacheRoleSummary），Handler 只做协议映射。创建只提交新 summary，删除只提交 summary 墓碑。角色三表延迟到后续进入世界的目标；无需角色初始化入口或共同提交。
- DataRoleSummary 新增可缺省的 IsDeleted，内容比较包含墓碑。单演示账号沿用 max+1，墓碑防止复用，溢出拒绝。
- 客户端现有 Session/Cache/LoginCache 在显式 V2 模式订阅 V2 协议，直接转换不可变展示数据；Session 在该模式阻止重复账号操作，不转换为旧协议。
- 独立 tests 场景使用测试工厂注册完整 V2 服务组，控制器复用角色页面；选角控件增加默认允许进入的开关，仅验收场景禁用。

## Risks / Trade-offs

- 成功响应后强杀可能丢失未写回摘要 → 保留现有显式保存、周期和关闭补写，文档说明限制。
- 多账号 max+1 会冲突 → 本批认证仅 demo，多账号编号由后续目标确定。
- 多连接页面无主动刷新 → 每次成功操作返回最新全列表，同账号 Actor 保证业务串行。
- 内存提交后的晚到响应仍可能被丢弃 → 不回滚服务端已接纳操作，重新登录读取权威列表。

## Migration Plan

增加独立验收入口，默认旧游戏不切换。生成协议代码与 UID；运行 .NET/Godot 和真实节点验收后同步规格、归档并本地提交。回滚提交即可恢复旧默认业务。
