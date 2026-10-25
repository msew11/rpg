# Proposal

## Why

当前客户端只有 BGM 音量设置，没有背景音乐播放。用户已指定登录和选角界面使用 M001、地图 1 使用 M015a；需要把曲目随页面和地图切换，并让地图曲目由配置承载。

## What Changes

- 导入 M001、M015a OGG 资源，登录和选角流程循环播放 M001。
- 地图配置增加可选 BGM 曲目编号，地图 1 配置为 M015a；进入角色世界时按地图配置播放并循环。
- 返回选角或登录时恢复 M001，离开场景时停止；现有 BGM 音量设置控制新播放器。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 页面和角色世界的背景音乐播放与切换。
- `client-config-map`: 地图配置中的背景音乐曲目编号。

## Impact

涉及音频资源、主场景播放器、地图配置模型/Schema、`map.json`、相关测试和开发文档。不改变现有短音效路由。
