# Proposal

## Why

用户要求调整系统菜单顺序，并追加一条提交。

## What Changes

- 从上到下依次显示“系统设置、返回角色选择、返回登陆、退出游戏”。
- 同步现有菜单顺序断言和操作定位，保持四项功能正常。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`：调整系统上拉菜单顺序与返回登陆条目的文字。

## Impact

修改 GameHud、现有客户端场景冒烟测试、路线图和界面规格。
