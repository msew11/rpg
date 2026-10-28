# Design

## Context

MockServer 当前同时持有会话、业务校验、DataServerRole 聚合以及存储入口。现有四表事务、地图分块文件和转移日志必须保留。

## Goals / Non-Goals

目标：无 IO 的请求入口、实体缓存唯一业务状态、完整的现有行为回归。范围不含协议、玩法或地图格式变更。

## Decisions

- RepoEntityCache 持有已加载的角色表组、摘要、待写领域、地图以及会话内存。RoleEntityTables 只组合平级 Role/Item/Equipment 缓存，不复制实体字段；候选替换后才发布内存及摘要。
- RepoServerRole 负责显式准备摘要、选中角色或删除用单例以及落地。Handler 无仓库、路径、发送委托或延迟加载能力。
- 请求按 Login/Role/Bag/Gm/World 分目录，单入口 Handle(cache, request)。共享背包操作在 Bag 模块，配置使用只读 ItemCfg 快照。
- 通知放入内存队列，由 MockServer 发送；Move 的成功响应延后生成，Pickup 的待完成意图由时间推进再次尝试，避免把计时或传输塞进 Handler。
- 地图对象迁入 Entity，规格和变更记录归 Map 模块，仅调整代码归属，保持 JSON 属性和事务。

## Risks / Trade-offs

- 延迟响应顺序变化风险 → 验收移动、拾取取消、重复请求及通知次序。
- 移除 Data 聚合影响测试构造 → 测试直接构造实体表组，不保留生产兼容包装。
- 内存和地图共同提交风险 → 先完整验证候选，再修改双方并建立共同落地关系；失败写盘不清 dirty。
