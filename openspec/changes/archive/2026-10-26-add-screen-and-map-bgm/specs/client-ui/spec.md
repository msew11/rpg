# Spec Delta

## ADDED Requirements

### Requirement: 页面与地图背景音乐
登录、选角及角色创建页面 SHALL 循环播放 M001；进入角色世界后 SHALL 循环播放当前地图配置的 BGM，地图 1 为 M015a。切回登录或选角时 SHALL 恢复 M001；切换到相同曲目 SHALL 不从头重播，场景退出时 SHALL 停止 BGM。BGM 音量 SHALL 使用现有 BGM 音量设置。

#### Scenario: 登录与选角
- **WHEN** 客户端展示登录、选角或角色创建页面
- **THEN** 播放 M001，页面间切换不重启同一曲目

#### Scenario: 进入地图 1
- **WHEN** 玩家选角成功并进入地图 1
- **THEN** 停止 M001，按地图配置播放 M015a

#### Scenario: 返回页面
- **WHEN** 玩家从角色世界返回选角或登录
- **THEN** 停止地图 BGM，恢复播放 M001

#### Scenario: 音量和退出
- **WHEN** 玩家调整 BGM 音量或退出主场景
- **THEN** 正在播放的曲目使用所选音量，退出后停止播放
