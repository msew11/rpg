# Design

## Context

MapCache 已原子校验 AOI 快照；角色通过缓存快照转 Action 进入 ECS。GroundItemsView 目前直接读取 AOI，拥有 UI 控件和输入状态。素材目录由 Items 目录中的解析器读取。

## Goals / Non-Goals

**Goals:** 动态地图对象与角色采用相同同步模式；UI 到 WorldView 单向依赖；共用素材处理仅留在静态 Util。

**Non-Goals:** 静态瓦片逐块实体化、改变协议或服务数量。

## Decisions

- WorldService 在固定帧比较 Session.Map 快照引用并提交 SyncMapObjectsAction。WorldManager 以 GUID 映射整数 entity，组件持有位置、版本和互斥载荷；读模型从组件生成，供世界表现与 UI 查询。这样沿用角色的权威缓存/运行状态分离，且避免增加业务协议监听。
- GroundItemsView 只持有世界节点，提供图标锚点和命中查询。UI 的 GroundItemOverlayControl 管理信息框、名称、避让和悬停；MainController 连接拾取、快捷键和世界轮廓。CanvasLayer 5 保持在世界和 HUD 之间。
- 素材目录 DTO 与静态读取/裁剪方法归 Utils。UI 与 WorldView 各自缓存所需目录或纹理；地面轮廓资源只归 WorldView。避免静态全局可变状态，也不增加服务。

## Risks / Trade-offs

- 缓存快照与 ECS 运行组件分别持有权威值和呈现值：与玩家模式一致；固定帧同步后才向表现提供新快照。
- UI 与世界变换不同：锚点由 WorldView 提供视口坐标，UI 每帧重新布局，保留已有避让规则。
