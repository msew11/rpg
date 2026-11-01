# Spec Delta

## ADDED Requirements

### Requirement: GM 当前 AOI chunk 高亮

GM 面板 SHALL 提供默认关闭的“显示当前 AOI”开关。开启后，客户端 SHALL 按已接纳的当前地图 AOI 块集合，以半透明青蓝色填充并描边每个有效块；中心块 SHALL 使用更明显的填充和边线。高亮 SHALL 位于绿色逻辑 chunk 边缘之上、粉色绘制区域边缘之下，不改变地图、AOI 订阅或输入。

#### Scenario: 首次显示与地图边角

- **WHEN** 玩家进入世界并开启当前 AOI 高亮，角色位于地图内部或边角
- **THEN** 仅高亮已接纳快照中的块，包括空块；中心块更醒目，边角没有地图外的虚构块

#### Scenario: 权威 AOI 改变

- **WHEN** 客户端接纳新的 AOI 差集或全量重同步
- **THEN** 高亮更新为最新中心块和订阅集合，离开范围的块不再高亮，不按预测位置提前改变

#### Scenario: 调试线层级

- **WHEN** 玩家同时开启逻辑 chunk 边缘、当前 AOI 和绘制区域边缘
- **THEN** AOI 高亮覆盖绿色边缘，粉色绘制区域线覆盖 AOI 高亮

#### Scenario: 关闭与离开世界

- **WHEN** 玩家关闭当前 AOI 开关或离开角色世界
- **THEN** 当前 AOI 高亮立即消失；重新进入世界时开关恢复默认关闭
