## ADDED Requirements

### Requirement: 角色与地面对象按坐标遮挡

角色和地面物品 SHALL 按等轴投影后的实体坐标 Y 绘制；Y 较大的实体显示在 Y 较小的实体上方。

#### Scenario: 角色经过地面装备

- **WHEN** 角色从装备坐标的上方移动到下方
- **THEN** 前后关系随实体坐标改变，同 Y 时顺序稳定
