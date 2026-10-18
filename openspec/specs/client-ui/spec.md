# client-ui Specification

## Purpose
TBD - created by archiving change data-driven-rpg-client. Update Purpose after archive.
## Requirements
### Requirement: 世界外 HUD 与面板
HUD 和系统面板 SHALL 独立于 ECS 世界，根据游戏配置与会话缓存显示游戏数据。

#### Scenario: 游戏 HUD
- **WHEN** 进入角色世界
- **THEN** 左上显示头像、等级、生命、魔力、经验，地图画面右上显示小地图雷达，底部显示一条 9 格技能栏，右下显示四个系统菜单按钮

#### Scenario: 菜单面板
- **WHEN** 点击角色、背包、大地图或系统按钮
- **THEN** 对应面板可打开；角色属性、大地图位置与系统设置可查看，背包显示空状态
