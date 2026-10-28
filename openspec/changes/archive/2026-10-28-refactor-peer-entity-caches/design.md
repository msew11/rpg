# Design

## Context

动机见 proposal.md。现有 RoleEntityCache 包含三个表以及 DataServerRole 双向投影；RepoServerRole 依赖这个聚合完成延迟保存和读取，角色单例缓存因此隐含拥有物品表。

## Goals / Non-Goals

**Goals:** 三个实体缓存各自拥有本表数据，RepoEntityCache 显式组合三者并维护跨表候选投影。

**Non-Goals:** 不改变 protobuf、四表 JSON、摘要懒加载、旧存档迁移及事务边界。

## Decisions

- RoleEntityCache 只保留 Value 和接受 RoleEntity 的构造方法；不再接收完整 DataServerRole，也不持有物品或装备缓存。
- RepoEntityCache 放在 Server/EntityCache，平级暴露 Role、Item、Equipment；支持从三个缓存或完整业务候选构造。相比直接在 RepoServerRole 中分散维护三个字典，这样继续保留整组候选的一次校验和替换边界。
- 临时业务 revision 与移动确认序号随聚合移至 RepoEntityCache；其 ToRole 继续生成既有不可变候选，序列化显式读取 Role.Value 及两个多例索引。
- EquipmentEntityCache 构造时借用 ItemEntityCache 检查共享主键，不保存或拥有物品缓存；保留现有完整性检查。

## Risks / Trade-offs

- [字段迁移遗漏影响缓存或落地] → 更新已有四表回滚、摘要加载和存档回归，并验证从独立缓存组合后的业务候选保留全部字段。
- [调用方仍将角色缓存视为聚合] → 搜索所有构造和类型引用，统一迁移到 RepoEntityCache。

## Migration Plan

无需存档迁移。完成构建与 tools/check.ps1 -Tests 后归档变更；通过本地提交整体回滚。
