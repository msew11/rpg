## MODIFIED Requirements

### Requirement: 地面物品定位点与图标错位分离

同点地面物品 SHALL 只错开图标及描边，实体坐标和绘制排序 SHALL 保持权威坐标；悬停和拾取 SHALL 跟随错开后的图标。

#### Scenario: 同点物品

- **WHEN** 两件地面物品具有相同坐标
- **THEN** 图标错开、锚点重合在真实坐标，两个图标仍可分别命中
