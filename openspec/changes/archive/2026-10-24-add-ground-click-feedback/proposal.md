## Why

当前客户端使用系统普通鼠标，地面点击没有独立反馈。使用用户指定的 Normal 普通鼠标与 circle 蓝色光圈，让玩家能看清点击位置，并将所需完整素材随项目发布。

## What Changes

- 全流程普通鼠标使用 Normal 首帧，热点沿用原配置 (2, 2)。
- 地面左键按下并提交移动后，在点击世界坐标显示完整 circle 光圈，短暂淡出；UI 点击不触发。
- 将原始 IANI 图块拼接为项目内 PNG，保留来源哈希并提供可重复导入工具。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ui`: 增加普通指针与地面点击反馈要求。

## Impact

MainController、世界表现节点、项目鼠标配置、素材导入工具及 Godot 场景验收。不改变移动协议或玩法规则。
