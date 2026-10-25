## MODIFIED Requirements

### Requirement: 世界外 HUD 与面板
GM 面板 SHALL 提供可输入的 0–100% 血条预览值。调整时左上生命条 SHALL 即时更新长度和颜色，角色缓存与服务端存档 SHALL 不变；当前 HUD 销毁后预览值 SHALL 消失。

#### Scenario: 临时调整血条
- **WHEN** 玩家在 GM 面板输入 50%、25% 或 75%
- **THEN** 左上血条分别显示橙、红、绿，长度对应输入百分比，角色生命原值不变
