# Design

## Context

当前 WorldManager 仅提供位置，ActorView 绘制占位圆形。外部 animation_frames.json 包含 26 个动作、605 帧及锚点；查看器定义了原始方向为左下、左、左上、上、下，另三个方向镜像。现有输入只有点击移动。

## Goals / Non-Goals

将性别和动作接到现有玩家 entity，保持 WorldManager 为唯一外部写入口。素材仅包含基础身体，不补绘头部、发型、装备和坐骑；不新增动作触发 UI 或战斗规则。

## Decisions

- DataRole 使用 RoleGender；演示数据显式设置男女，旧构造调用默认男性以兼容。DataRoleComponent 保存性别；DataActorComponent 保存 RoleMotion 和逻辑方向。WorldManager 暴露不可变 DataActorState 快照，节点不能取得可写组件。
- MovementSystem 按实际移动更新 Run、方向，到达后 Stand 并保留方向；零时间步不产生虚假移动。SetRoleMotionAction 支持其他已知动作，清除移动目标，持有动作直至后续命令。播放完成只停末帧，不决定逻辑状态。
- 同步性别按权威 revision 判断，位置继续按确认序号过滤，避免无新移动确认时丢失性别更新。
- ActorView 接收快照并投影位置，将逻辑方向投影后量化为八方向；AnimatedSprite2D 负责时间，子节点水平缩放处理镜像，以锚点平移保持脚部定位。相同动作不逐帧重启，单纯改向保留播放进度，动作或角色身份改变才重置。
- 将原始透明 PNG 按每方向一行无损打包为 26 张图集，生成包含 SpriteFrames 和锚点的 DataActorAnimation .tres。RepoActorAnimation 在表现层按性别、动作加载并缓存。相比运行时读取外部 JSON，原生资源依赖可被 Godot 正常导出；不引入新依赖。
- 工具脚本从显式传入的外部目录重建资源，保留来源说明与帧元数据校验，不提交 GSN、预览和外部绝对路径。
- 播放暂用查看器的每秒 10 帧；站立、跑步循环，其余一次播放后保持末帧。该速度仅为显示参数，不作为玩法节奏。

## Risks / Trade-offs

- [方向来自预览映射] → 使用实际帧和八方向验收，不猜测未解码的原始游戏语义。
- [基础身体不含头部和装备] → 明确资源边界，后续分层装扮另立 change。
- [渲染错误难由纯逻辑测试发现] → Godot 集成检查验证真实资源、动画进度、锚点、镜像和切换；保留现有客户端烟测。

## Migration Plan

更新角色数据与节点绑定后运行完整检查，归档 specs，并在 develop 本地提交；可用该提交回滚。
