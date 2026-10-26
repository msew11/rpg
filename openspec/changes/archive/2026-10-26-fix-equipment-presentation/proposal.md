# Proposal

## Why
装备和背包的拿起图像、名称提示不一致，双手剑与拳刃绑定了错误素材，长柄武器后段覆盖人物；所有人物展示的默认朝向和旋转方向也需按用户截图统一。

## What Changes
- 以背包为准共用拿起图像与即时道具信息浮窗，装备窗口关闭和刷新时清理提示。
- 修正双手剑、拳刃持握系列，长柄武器在握点分层。
- 所有展示默认朝屏幕右侧，保留按钮图案并修正人物顺逆旋转。

## Capabilities

### New Capabilities
无。

### Modified Capabilities
- `role-equipment`：装备拿起和即时道具信息与背包一致。
- `client-actor-presentation`：正确的装备外观与长柄后段遮挡。
- `role-management`：人物展示默认侧身与旋转方向。

## Impact
修改 UI 共用展示、装备素材导入和角色内部绘制，补充真实输入与图形回归验收；协议和存档不变。
