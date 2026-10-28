# Design

## Context

现有十一种请求已经分成登录三种、角色四种、世界四种，但共用上下文，且世界 Handler 声明在 RoleActor。参考 proposal.md 的动机。

## Goals / Non-Goals

以编译期类型限制 Handler 所属缓存。保留世界邮箱中的角色独占工作副本和共同物品事务，不引入通用 Context 基类或兼容重载。

## Decisions

- 每个 Actor 用 readonly 字段声明所属 Handler；Handler 目录与 Actor 类型对应。Login 处理登录/创建/删除，Role 处理移动/GM/背包移动/装备，World 处理选角/AOI 重同步/丢弃/拾取。
- 三个独立 Context 均以只读 EntityCache 属性绑定各自缓存，共享不可变 DataServerRules。LoginContext 只管理登录列表，RoleContext 只管理单角色，WorldContext 持有地图缓存以及此次事务移交的 RoleContext 工作副本。
- RoleActor 将世界协议消息和角色副本交给 WorldActor，由其自行调用地图 Handler。内部角色与世界操作使用不同消息类型与 Func 签名，删除 InWorld 布尔分派。
- Handler 保持单一 Handle(具体Context, 请求) 入口。测试检验三组 Handler 的 Context 类型、Actor 声明归属、缓存身份、锁盘下纯内存处理和跨 Actor 副本隔离。

## Risks / Trade-offs

- 拆分易漏掉通知队列或模拟状态 → 复用 RoleContext 通知队列并完整回归移动/拾取/AOI/物品共同恢复。
- 静态规则重复构建 → Actor 共用一次校验的只读规则，Context 不创建其他领域的空缓存。
