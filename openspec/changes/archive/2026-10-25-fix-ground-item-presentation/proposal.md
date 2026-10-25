# Proposal

## Why

地面物品按透明像素命中导致狭长物品难以悬停，且误用背包大图。两套素材均已导入，应明确区分。

## What Changes

- 图标矩形向外扩展 8 像素作为悬停和点击范围，真实 alpha 轮廓保持不变。
- 配置增加可选 groundAssetId，七件装备按导出表 icon_small_code 绑定 item/item。
- 缺地面素材使用占位，不回退背包图标。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `ground-item-pickup`: 放宽命中范围，保留轮廓和 UI 隔离。
- `client-item-config`: 独立配置地面素材。

## Impact

修改 ItemCfg、Schema、配置、GroundItemsView 和拾取冒烟测试。不修改协议、服务数量、存档或权威拾取规则。
