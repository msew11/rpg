## MODIFIED Requirements

### Requirement: 角色快捷键自定义

快捷键 SHALL 提供角色 C、道具 B、地图 M、地面物品信息 Z 四项默认绑定，并按角色持久化。点击一项 SHALL 高亮并监听键盘；合法且不冲突的键 SHALL 保存、立即生效并解除高亮。允许字母、数字、F1–F12 及 Ctrl/Alt/Shift 组合；禁止 Meta、系统组合键 Alt+Tab/Alt+F4/Ctrl+Alt+Delete、其他操作键与重复绑定。非法输入 SHALL 被消费、给出提示并保持高亮等待，关闭窗口或切换页签 SHALL 取消等待。

#### Scenario: 地面物品信息快捷键

- **WHEN** 玩家按 Z 显示信息、再次按 Z 隐藏，改绑后重进角色，并在聊天输入原绑定
- **THEN** 新绑定按角色恢复并能切换，旧键与聊天输入不切换信息
