# Spec Delta

## ADDED Requirements

### Requirement: 地图背景音乐配置
地图配置 SHALL 提供可选的背景音乐曲目编号。地图 1 SHALL 配置为 M015a；配置编号 SHALL 是合法的曲目标识，未配置时该地图不播放地图 BGM。

#### Scenario: 地图 1 曲目
- **WHEN** 客户端加载项目自带的地图配置并读取地图 1
- **THEN** 地图 1 的背景音乐曲目编号为 M015a

#### Scenario: 未配置曲目
- **WHEN** 地图配置没有背景音乐曲目编号
- **THEN** 该地图仍可加载，进入该地图时不播放地图 BGM

#### Scenario: 无效编号
- **WHEN** 地图配置包含非法的背景音乐曲目编号
- **THEN** 启动时报告地图配置错误
