## Why

背包已具备物品快照与展示，需要通过拖动调整格子，并支持同类堆叠合并与占用格交换。

## What Changes

- 新增 MoveItem/MoveItemRt，服务器按角色、实例、容量和背包版本校验后原子保存。
- 空格整份移动，同类未满堆叠优先合并，剩余留原格；其他情况交换，不提供主动拆分。
- 原生拖放显示预览，成功后以既有完整背包推送刷新，失败保留原状态并提示。

## Capabilities

### New Capabilities

### Modified Capabilities
- `role-bag`: 拖放移动、堆叠合并、交换、持久化与失败反馈。

## Impact

bag.proto、协议注册、SessionService、MockServer、背包控件与独立格子控件、测试和长期文档。不改变现有存档字段或其他背包功能。
