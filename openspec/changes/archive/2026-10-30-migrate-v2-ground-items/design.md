# Design

## Context
已确认按最新权威状态执行，沿用运行时身份隔离与单实例物品信号量。

## Goals / Non-Goals
迁移既有丢弃、销毁、拾取和地图显示规则；不引入新玩法或主动拆分。

## Decisions
- bag.proto 新增 DropItem/DropItemRt、PickupItem/PickupItemRt、仅 success 的 PickupItemResultNotice；map.proto 增加 upserted_objects=4、removed_object_ids=5。
- actor/role.proto 增加 AdvanceRoleRt.pickup_ready=2、CompleteRolePickup/CompleteRolePickupRt；actor/world.proto 直接 CreateGroundItem、GetGroundItem、RemoveGroundItem 请求回复。
- Role 构造合法候选，再直接调用 World 创建或删除；World 不问 Role。共享信号量在邮箱外取得，持有至本人通知与最终结果输出。
- Pickup 立即回复，运行时继续调度 pending；时钟完成角色推进后经 Login→Role 完成转移。新有效移动与控制屏障静默清除 pending；背包变化不取消，完成时验证最新容量。
- World 观察基线保存可见对象，交集发送对象变化，新进入完整块、离开隐式删除；静止观察者由对象变化标脏。
- 专用单批次保存先捕获不可变角色、地图、账号变化，再写旧字节回滚记录，保存三表、地图清单与摘要，删记录作为完成点；地图旧块清理延后。重启在任何业务读取前恢复；失败保留内存及待写，成功仅接受捕获值，移动期间的后续变化仍待写。EnterWorld 先完成共用批次，避免单角色提前落地未保存的转移。

## Risks / Trade-offs
单实例物品及保存串行，保存期间物品等待。强杀允许丢失尚未保存的成功操作，但不允许落盘两侧重复或丢失。
