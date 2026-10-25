## Why

当前丢弃总会在角色脚下生成地面物品，用户需要通过 Ctrl 丢弃直接销毁指定数量。销毁必须先显示明确确认，避免误操作造成物品永久丢失，并保持背包权威校验及持久化规则一致。

## What Changes

- 拿起物品后按住 Ctrl 点击地图，打开销毁数量确认；单件也须确认。
- 确认后服务端扣除指定数量，不生成地面对象；取消不修改状态。
- 普通丢弃保持现有单件直接、多件确认的行为。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `ground-item-drop`: 增加 Ctrl 销毁确认与权威扣除。

## Impact

修改背包界面、Session 丢弃接口、DropItem 协议与 MockServer，补充协议和真实输入验收。沿用背包延迟存储，无新增依赖。
