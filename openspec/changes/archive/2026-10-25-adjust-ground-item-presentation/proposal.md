## Why

脚下丢弃容易被角色遮挡，地面常驻名称也增加画面负担。按用户要求改为周围留出避让距离后丢弃，并与背包共用悬停名称。

## What Changes

- 服务端在角色周围选择落点，排除过近及角色身体遮挡区域，地图边缘优先选择地图内候选点。
- 地面不常驻显示名称，悬停图标时即时显示配置 name，复用背包名称浮层。
- 保留数量标记、轮廓、Fetch、拾取和共同落地行为。
- 角色与地面物品按实体坐标排序；GM 可显示并调整实体锚点标记颜色。
- 丢弃落点更靠近角色，随机半径与角度使用连续坐标，避免整齐环状排列。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `ground-item-drop`: 周围权威落点与共享悬停名称。
- `ground-item-pickup`: 命中范围只使用图标扩展矩形。
- `client-actor-presentation`: 角色与地面物品按等轴坐标遮挡。
- `client-ui`: GM 实体锚点显示与颜色控制。

## Impact

MockServer、BagControl、GroundItemsView、共享名称控件及现有服务端和 Godot 验收；无协议变更。
