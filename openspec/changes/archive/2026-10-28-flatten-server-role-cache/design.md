# Design

## Context

RoleEntityTables 是三份不可变实体缓存的组合，ServerRoleCache 还保存可变 Dirty 和 ServerSession。Actor 通过复制会话和替换实体候选保证请求失败不污染原状态。动机见 proposal.md。

## Goals / Non-Goals

目标是删除中间容器并保持 mock-server、server-actors、server-write-back 现有行为。协议、持久化格式、游戏规则不变。

## Decisions

- ServerRoleCache 构造时接收完整 Role、Item、Equipment 并校验跨表约束，Actor 在 INIT 完成前不暴露未加载实例。保留平级实体缓存，不展开复制实体字段。
- WithRole、WithItems 在 ServerRoleCache 中创建候选；Copy 共享不可变实体缓存并复制会话、可写集合及待写状态。候选构造不改变原缓存，物品替换保留现有装备扩展、新增扩展及删除关联。
- RoleContext.Commit 在完整候选校验后替换当前缓存中的三份实体引用，保留当前会话对象及已登记 Dirty，仍按领域过滤。使用受控内部方法替换，防止调用方逐表任意赋值。
- 仓库、投影与 Actor 消息直接使用 ServerRoleCache。实体内容比较忽略会话和待写标记，维持现有持久化比较语义。
- 不保留 Tables 兼容属性，也不新增另一种实体组容器。

## Risks / Trade-offs

- 原先共享不可变表组改为可变缓存 → 登录新建数据移交及 Actor 工作回合复制容器和会话，保持所有权隔离。
- 提交时替换会话会丢失移动确认等后续修改 → 提交仅替换实体引用，补充回归测试验证会话身份和领域标记。
- 跨表校验遗漏会破坏存档 → 保留编号、格子、装备槽和扩展主键约束，复用现有存档、装备与共同恢复测试。

## Migration Plan

一次替换所有代码与测试调用并删除旧类，更新架构与路线图。无需存档迁移；如需回滚可撤销本次本地提交。
