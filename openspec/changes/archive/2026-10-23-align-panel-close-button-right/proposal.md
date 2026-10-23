# Proposal

## Why

面板的关闭按钮目前紧挨标题。用户要求将关闭按钮移到右侧，以便在统一位置关闭面板。

## What Changes

- 角色、背包、大地图、GM 和系统设置面板的标题保持左对齐，关闭按钮靠标题栏最右侧。
- 标题栏随面板宽度变化自动分配空间。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`：明确各面板关闭按钮位于标题栏右端。

## Impact

仅修改 `GameHud` 共用面板标题栏布局，更新界面规格与路线图。复用现有关闭行为和场景冒烟测试。
