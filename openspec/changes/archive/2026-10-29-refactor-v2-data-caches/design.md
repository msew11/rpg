# Design

## Context

三个模块现由 Context 直接管理数据、修改和保存基线。用户明确要求改为可变工作区，状态职责集中到对应缓存。

## Decisions

- Context 只新增一个所属 Cache 引用，保留基类借用的 Config、Actors；不提供数据或 Commit 转发。
- LoginCache、RoleCache、WorldCache 组织 DataCache，负责身份及聚合导出、聚合提交和保存完成通知。
- DataCache<T> 管理当前值、成功保存基线和内容比较；DataCacheArray<T> 统一默认数组为空及顺序内容比较。按数据提供 DataCacheRoleSummary、DataCacheRole、DataCacheItem、DataCacheEquipment、DataCacheWorldObject。
- 摘要创建、删除、可见列表和深层装备比较归 DataCacheRoleSummary；地图分块差异归 DataCacheWorldObject。角色三表数据缓存不新增业务校验。
- 可变的是工作区持有的当前值；值继续采用不可变 record/数组。DataLoginSnapshot、DataRoleSnapshot、DataWorldSnapshot 仅作仓库导出和聚合提交值，不能承担运行时状态。
- IO 留在 Actor 的邮箱回合。失败不接受保存基线，成功后通过 Cache 通知全部 DataCache。协议、仓库接口和存档格式不变。

## Risks and verification

直接修改子缓存时，聚合脏状态必须即时反映；恢复原值必须取消待写。覆盖子缓存修改、导出隔离、默认数组、摘要墓碑及地图跨块移动；沿用保存失败、重试、关闭补写和真实按钮验收。

## Open Questions

无。
