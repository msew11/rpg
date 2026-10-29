# Proposal

## Why

WorldActor 当前只有地图身份，无法在首条消息前恢复动态对象，也未参与 V2 写回。为已确认的地图数据闭环补齐独立存储，不改变旧客户端或 Actor 通信机制。

## What Changes

- 从已加载配置复制不可变地图规格，INIT 完整恢复对象后发布 Context。
- 引入独立 World 数据与仓库，按块比较保存基线，支持修改、移动、删除和恢复原值。
- 分块版本文件加清单原子提交，失败重试、孤立候选隔离与提交后清理。
- 保存顺序扩展为 Role→World→Login，接入周期、显式屏障和关闭补写。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `server-v2-storage`：地图按需加载、分块提交与三组保存顺序。

## Impact

涉及 Modules/World、模块装配、MockServerService、存储及 Godot 集成测试和 V2 架构文档；不迁移旧存档，不实现 AOI、怪物、拾取、创角、首次入角、正式业务协议或跨 Actor 事务。
