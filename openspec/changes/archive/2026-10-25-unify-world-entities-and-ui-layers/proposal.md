# Proposal

## Why

当前地面物品只有 AOI 缓存和表现节点，没有 ECS entity；地面信息框又由 WorldView 创建和管理。这与动态世界对象统一进入 ECS、UI 高于 WorldView 的边界不符。

## What Changes

- AOI 中的独立动态对象映射到稳定的本地 ECS entity，由 Action 同步。
- 地面图标与命中留在 WorldView；名称、信息框、布局和点击归 UI。
- 共用图集解析归静态 Util，移除顶层 Items 目录。

## Capabilities

### Modified Capabilities

- `client-ecs-world`: 地图动态对象的 ECS 生命周期。
- `ground-item-drop`: 地面物品的 ECS 呈现与 UI 信息层。

## Impact

影响 World、World/Views、UI、素材工具、相关测试与架构文档。协议、存档和服务数量不变。
