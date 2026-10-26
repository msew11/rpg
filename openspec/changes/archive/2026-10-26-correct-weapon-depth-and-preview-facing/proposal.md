# Proposal

## Why
上一轮将长柄后段固定放到身体后方，未覆盖背面应遮挡身体的视图；角色展示需按用户明确的↘而非→默认朝向。

## What Changes
- 枪和木杖后段随方向及动作切换前后层，背面遮住身体，正面由身体遮住。
- 选角、创角及装备窗口默认朝↘，保留已修正的旋转方向和朝向记忆。

## Capabilities
### New Capabilities
无。
### Modified Capabilities
- `client-actor-presentation`：长柄后段按方向遮挡。
- `role-management`：默认朝右下。

## Impact
仅修改预览默认值、角色内部排序、对应回归及文档，不改变素材像素、协议或存档。
