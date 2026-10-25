## Why

角色、道具和地图窗口需要记住玩家的位置偏好；角色界面偏好与系统设置需要分离，并允许自定义打开窗口的快捷键。

## What Changes

- 三个窗口支持标题栏拖动，松开后按角色保存相对视口坐标，重开、重选和重启恢复，并限制在视口内。
- 系统菜单增加角色设置，原角色 HUD 迁入其“界面”页签，新增“快捷键”页签。
- 角色 C、道具 B、地图 M 默认绑定；点击绑定高亮后捕获键盘，合法且无冲突时保存并退出，非法时拦截且保持等待。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 系统菜单分类、弹窗默认位置和 HUD 调整入口。
- `client-settings`: 角色窗口位置、设置分类与快捷键持久化。

## Impact

SettingService、RoleSetting、GameHud、ViewportPanelControl、BagControl 及真实输入验收；无协议或服务器改动。
