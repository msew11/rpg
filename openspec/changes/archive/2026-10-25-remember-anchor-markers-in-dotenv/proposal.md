## Why
GM 的物体锚点显示开关只在当前世界有效，重新进入角色或重启后会恢复关闭。需要与控制台日志开关一致，记住用户的调试显示选择。

## What Changes
- 在 `user://.env` 保存并读取 `RPG_SHOW_ENTITY_ANCHORS`；缺失或非法值默认关闭。
- GM 切换时立即保存并更新世界，失败时恢复控件并显示原因。
- 建立角色世界时应用当前开关，保留现有黄色默认颜色及运行期改色能力。

## Capabilities
### Modified Capabilities
- `client-ui`: 锚点显示开关跨世界与启动恢复。

## Impact
EnvService、GM 面板与世界初始化、锚点测试和客户端文档。
