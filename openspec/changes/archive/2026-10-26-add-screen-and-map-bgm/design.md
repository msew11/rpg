# Design

## Context

主场景已有 `UiSoundPlayer` 和独立的 BGM 音量总线，但没有背景音乐播放器。`MainController` 集中建立登录、选角和世界页面；`WorldService.MapConfig` 提供当前角色地图配置。地图配置由 `MapCfg`、`map.json` 和 JSON Schema 共同定义。

## Goals / Non-Goals

**Goals:** 页面与地图切换时循环播放指定曲目，复用现有 BGM 音量设置，并把地图曲目留在地图配置中。

**Non-Goals:** 配置其他地图的曲目、交叉淡入淡出或改变短音效播放。

## Decisions

主场景新增一个长期存在的 BGM 节点，持有单个 `AudioStreamPlayer`，使用 BGM 总线。`MainController` 在展示登录或选角时请求 M001，在世界初始化并取得当前 `MapConfig` 后请求其 `BgmTrackId`；曲目相同则保持播放进度，节点离树时停止。

地图 JSON 使用可选 `bgmTrackId` 字段，Schema 限定安全的曲目编号格式，地图 1 写入 M015a。未配置时播放器停止；这样其他地图可逐张配置而不推定曲目。

## Risks / Trade-offs

OGG 资源缺失或损坏会让对应页面无音乐；播放器加载时给出明确错误，测试验证导入、配置读取和切换状态。新曲目不做淡入淡出，切换时直接停止前一首。
