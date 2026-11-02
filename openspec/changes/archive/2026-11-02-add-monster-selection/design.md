# Design

## Context

MonstersView 已按服务器 GUID 复用 Sprite2D；MainController 在未消费输入阶段按放置、拾取、移动顺序处理；PointerCursor 管理自定义硬件指针。原版 magic9002.gso 是 DIMG v1 六帧，画布 69×43、公共锚点 (34,20)；AttackH.gsa 是 40×40 单帧，uiroot.lua 热点 (2,2)。

## Goals / Non-Goals

选择是表现层临时状态，不进入 ECS、服务或服务器。继续借用不可变 AOI 对象快照，避免引入第二份怪物业务数据。不实现攻击或目标属性。

## Decisions

- MonstersView 保存唯一 SelectedObjectId、HoveredObjectId，按绘制 Y 顺序及同位稳定顺序查询 Sprite2D 的局部矩形和 alpha。每帧根据视口鼠标位置与实时画布变换重新命中，GUI、模态和失焦阻止交互。
- MainController 在放置之后、拾取和移动之前截获左键怪物；右键仅在未命中怪物与地面物品时取消。GM 放置期间禁用怪物命中，放置取消后恢复。
- 唯一 AnimatedSprite2D 附在选中 Sprite2D 的脚底，ShowBehindParent 保证贴图覆盖圆环；使用原公共锚点、不裁切，按怪物贴图宽度等比放大（最小原尺寸），避免树根遮住整个圆环。每帧 0.1 秒为本项目播放参数，不声称复原原版速度。重复选择不重启动画。
- PointerCursor 在 HUD/窗口拖动之后、地面物品之前读取怪物悬停并使用 AttackH，阻止普通 Click 反馈覆盖剑形鼠标。
- 独立工具严格顺序解码六个 DIMG 像素块、校验尾部帧数，导出六帧 PNG 与来源哈希；复用现有 IANI 解码导出剑形指针。运行时只读取项目内 PNG。

## Risks / Trade-offs

- 首次像素命中读取图像有开销 → 按纹理缓存 CPU 图像，不逐帧提取。
- UI 与世界更新先后可能残留一帧鼠标 → 指针读取时刷新实际命中，输入直接用事件坐标查询。
- 离开世界延迟释放可能保留圆环 → ClearGame 同步重置选择和悬停，再释放世界节点。

## Migration Plan

无需迁移存档或协议；回滚本地提交即可撤销。
