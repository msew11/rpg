## Context

当前 RoleWorldStatePb 是扁平结构，MoveRt 与 RoleStateNotice 都使用整份状态。RoleCache 用一个 revision 过滤所有更新，世界也用单一版本过滤。

## Decisions

- RoleWorldProfilePb 暂含除位置以外的角色字段，RoleWorldPositionPb 含地图、坐标、版本与移动确认序号；RoleWorldStatePb 组合两者。
- 服务端仍维护单一递增权威版本。客户端分别记录最后接纳的资料和位置版本，完整快照逐部分合并。
- 普通移动只发 MoveRt.position；RoleProfileNotice 与 RolePositionNotice 供主动局部更新。RoleStateNotice 保留完整快照。
- RoleCache 内部分别保存不可变资料和位置，组合成已有 RoleWorldStatePbData 供 UI 与世界读取；世界分别过滤资料与位置版本。
- 不兼容旧网络消息，角色存档格式不变；其他玩家位置广播留给后续多人实体功能。

## Verification

验证协议往返、移动载荷、交错版本、旧消息过滤、主动推送与世界纠偏，并执行项目检查。
