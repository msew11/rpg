# Proposal

## Why

角色装备窗口的头像、放大预览、槽外标题和操作提示占用空间。用户要求紧凑展示，并统一通过 Ctrl＋左键自动穿脱和图标旋转。

## What Changes

- 装备窗口中央人物不显示头像，按地图人物的 1 倍比例展示。
- 槽位名称以灰色文字放在槽内，不再显示上方标题；移除穿脱操作说明。
- Ctrl＋左键点击背包装备自动选择适配槽穿戴或替换，点击已穿装备自动卸下；取消右键卸下，保留普通左键拿起放置。
- 角色、选角和创角的顺逆旋转统一使用箭头图标。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `role-equipment`：紧凑预览、槽内名称、Ctrl 快捷穿脱与输入隔离。
- `role-management`：顺逆旋转入口改为统一图标，保留八方向行为。

## Impact

修改 CharacterEquipmentControl、RolePreviewControl、BagControl 及格子输入入口，添加本地 SVG 旋转图标，补充现有场景验收。复用现有服务器穿脱协议、分类适配和权威快照。
