# Design

## Context

见 proposal.md。现有角色移动经 MoveAction/Move 协议预测、服务器每 Tick 推进实际位置。物品转移已有 RepoServerItemTransfer，地面表现只读地图缓存且不参与 ECS。

## Goals / Non-Goals

实现闭环拾取和真实 alpha 轮廓反馈，复用既有分组提交和九服务依赖。不引入拾取专用 ECS、寻路框架、部分拾取或网络事件总线。

## Decisions

- World.PickupItem 查询唯一地图缓存中的对象，先按原流程提交 MoveAction/Move，再通过 Session 发送 PickupItem（角色、GUID、对象版本、背包版本、move_action_id）。MainController 只协调地面命中及反馈，不计算距离和物品表。
- MockServer 验证当前地图、AOI、地面类型、版本、移动 ID 和目标匹配。远处请求只保存一个待拾取意图，每 Tick 在移动推进后以权威位置检查，距离不大于 TileSize 时整份转移。新的合法 Move 取消旧意图，切角/登录丢弃意图，目标/背包版本变化报失败。玩家仍走到点击物品的位置，拾取可在最后一个瓦片内发生。
- 将已有 GM 入包规则抽到 DataServerBagRules.Add，统一堆叠、空格、编号和容量原子拒绝。完成时移除对象、保存候选背包并登记现有提交组，再发背包通知、AOI 移除及最终响应。复用文件事务，不改文件格式。
- GroundItemsView 保持纯表现，提供视口坐标命中查询和悬停对象身份。按 alpha 裁剪的显示尺寸遮罩生成两像素膨胀外轮廓纹理，缓存资源而非业务状态；透明角落不命中，名称区域可命中。UI 覆盖和模态窗口阻断命中，摄像机变化重新投影查询。
- PointerCursor 读取现有表现组的悬停状态，沿用 Fetch 两帧、缩放及热点。HUD 调整优先，其次物品，最后普通点击；移出、失焦和节点释放清理。
- BagControl 单件直接调用已有 Session.DropItem，多件保留数量确认，操作仍先于 MainController 未消费地图点击处理，避免拿着背包物品时误拾取。

## Risks / Trade-offs

- 物品接近期间背包或目标改变 → 拾取版本校验失败，明确提示且不部分转移。
- 重叠图标和透明角落 → 沿用同落点错开布局，按反向绘制顺序及 alpha 命中，一次只选一个对象。
- 现有同步文件持久性和单进程边界保持；失败重试和恢复测试覆盖反向转移与同周期往返。
