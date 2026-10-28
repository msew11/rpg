# Design

## Context

见 proposal.md。现有 RepoServerRole 缓存聚合角色并落 role/bag，客户端事件只交付业务消息。用户确认保持地图对象版本及其持久化契约。

## Goals / Non-Goals

**Goals:** 实体表负责落地，摘要负责列表，私有实例负责穿戴，公开配置编号负责展示；保留穿脱、丢弃拾取、延迟落地及事务恢复。

**Non-Goals:** 不新增属性、槽位玩法、多人广播或地图存储格式。

## Decisions

- Entity / EntityCache 分别放 src/Server/Entity 和 src/Server/EntityCache。RoleEntityCache.Value 保存单个 RoleEntity；ItemEntityCache.Items 和 EquipmentEntityCache.Items 按 InstanceId 索引。DataServerRole 仅作现有业务候选聚合，通过实体缓存投影，避免重写已验证的物品规则。
- role.json 为单例对象；item/equipment 为纯数组；role_summary.json 为展示对象。角色保存增加行数及 NextInstanceId，装备扩展当前只有 InstanceId。穿戴属于 ItemEntity，slot_index 为 0，客户端槽位从静态分类推导；ItemEntity 仅在服务端另存权威装备位置，以保持仓库不依赖客户端配置且兼容旧手持迁移，不投影到 ItemPb。
- 列表先加载摘要字典；仅旧格式缺失摘要时读取完整旧数据生成内存摘要，下次保存迁移。新格式缺失/损坏不回退备份。删除以摘要墓碑保留编号。
- 原子替换与回滚扩展固定领域 role/item/equipment/role_summary；物品转移外层事务覆盖这些文件，地图 manifest/chunk 格式不变。
- 每个网络发送端持有不落地的递增 revision，写入 EnvelopePb。解码后以进程内元数据供转换层读取；缓存继续按领域记录最后接纳的信封版本，不在业务 protobuf 增加版本字段。预期物品版本仍作并发条件，服务端校验最近物品快照的信封版本；地图对象版本不变。
- BagPb.Items 包含未穿和已穿实例；UI 背包只绘制未穿记录，装备格从私有实例和配置推导，世界及选角只按公开配置 ID 绘制。ItemPb oneof 显式携带空 EquipmentPb，为后续属性留位。

## Risks / Trade-offs

- 协议不兼容旧二进制 → 同次更新生产者、消费者和生成代码。
- 多表提交中断 → 白名单回滚记录及故障测试。
- 同类双戒指无法仅按分类区分 → 本次仍仅主副手可操作，其余保持占位。

## Migration Plan

旧单文件和 role/bag 仍可读；登录不提前写盘，首次正常保存写完整实体表及摘要，保留旧备份。新格式摘要存在后严格加载实体文件，不恢复旧数据。角色及物品 revision 不迁移；实例编号、位置、数量、穿戴及 NextInstanceId 保留。
