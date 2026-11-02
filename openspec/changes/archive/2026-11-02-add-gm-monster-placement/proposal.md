# Proposal

## Why

目前地图协议与存档已有无行为怪物载荷，但缺少怪物配置、展示素材和可实际放置的 GM 入口。开发时需要在地面选定坐标后再向服务器创建怪物。

## What Changes

- 新增以正 Int32 为主键的怪物 JSON 配置及 Schema，首条为树妖，复用本地导出的树妖站立首帧与原始锚点。
- 聊天输入 `/monster` 显示配置怪物列表，上下键或鼠标选择，回车进入与实物等大的鼠标虚影预览，共享世界相机缩放和地面锚点。
- 只在左键点击合法地面后发送 `monster <配置ID> <逻辑X> <逻辑Y>`，直接使用虚影逻辑坐标，保证放置后贴图与虚影同尺寸同位置；单次放置结束，右键取消不发送。
- 服务端通过既有 Login→Role→World 创建权威对象，沿用地图 AOI、ECS 和延迟落地；客户端展示树妖贴图。

## Capabilities

### New Capabilities

- `gm-monster`: 怪物配置、延迟发送的放置交互、服务器创建及静态展示。

### Modified Capabilities

无。复用 map-aoi 与 server-map-state 的现有同步、存储契约。

## Impact

影响 ConfigService、ChatControl、MainController、世界表现和 GM Actor 处理链；仅增加内部 CreateMonster 协议，公共 Gm 与地图协议保持现有字段。增加配置和真实输入回归，更新架构与路线图；不增加 AI、战斗、属性投放或刷怪规则。
