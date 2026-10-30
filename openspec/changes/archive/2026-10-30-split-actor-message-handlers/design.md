# Design

## Context

见 proposal.md。Login、Role、World 共五个聚合 Handler；AdvanceRoleHandler 与 MoveHandler 同文件。注册集中在 MockServer 和 ServerModules，AOI 测试直接调用聚合类。

## Goals / Non-Goals

目标为按请求独立类型、文件和目录，保留原业务顺序、异常、协议和串行邮箱。此次不改变玩法、消息或落盘边界。

## Decisions

- 原方法体逐消息迁移；Role 的 Gm、MoveItem、GetRoleItems、PickupItem 属于 Handler，其余跨 Actor 消息归 Async。World 全部本地处理归 Handler，Login 物品转发归 Async。
- 账号归属、摘要、物品规则、角色数据投影、拾取就绪、世界帧与初始快照构建及地面对象辅助类归各自模块 Utils；跨 Actor 的角色投影发布归 Common/Utils。Handler 目录只放 Handler，不将共用行为挂在具体消息 Handler 上。
- 更新显式注册，不引入反射自动注册。结构回归检查单契约与文件归属，既有业务测试验证调度、AOI 和物品行为。

## Risks / Trade-offs

- 拆分后遗漏注册或共用辅助调用 → 全量编译、结构回归及既有业务验收。
- 文件移动影响 Godot UID → 已有辅助逻辑或处理器迁移尽量保留原 UID，新文件由 Godot 导入生成。

## Migration Plan

本地 develop 原地修改；无数据迁移。以单个约定式提交交付，可整体回滚。
