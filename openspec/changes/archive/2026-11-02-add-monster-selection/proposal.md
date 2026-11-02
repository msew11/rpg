# Proposal

## Why

已投放怪物缺少选中反馈。用户需要左键选中、右键点地取消、红色脚底圆环与红色剑悬停鼠标，仅确认目标而不攻击。

## What Changes

- 左键点击怪物选中或切换唯一目标，右键点击空地取消；普通左键点地仍可移动并保留选择。
- 选中目标脚底循环播放 magic9002 的六帧红色圆环，悬停怪物显示 AttackH 红色剑。
- 隔离 GUI、模态窗口和 GM 放置输入，处理相机变换、目标移出 AOI、离开世界与失焦。
- 只添加客户端反馈，不增加攻击、服务器消息或持久化状态。

## Capabilities

### New Capabilities

- `monster-selection`: 怪物的本地单目标选择、取消、动画和指针反馈。

### Modified Capabilities

无。

## Impact

涉及 MonstersView、MainController、PointerCursor、资源导入工具、场景验收和长期文档；服务器与协议保持现有行为。
