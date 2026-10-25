## Why

角色窗口底部的“切换角色”按钮与系统菜单中的“返回角色选择”重复，用户要求移除角色窗口入口。

## What Changes

- 角色窗口只展示角色资料，移除“切换角色”按钮及其点击回调。
- 保留系统菜单“返回角色选择”及现有会话返回流程。
- 增加角色窗口验收，确认按钮不存在。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 角色窗口不再提供切换角色操作。

## Impact

仅修改 GameHud 的角色窗口内容、UI 场景验收及对应规格和路线图；不改会话服务。
