## MODIFIED Requirements

### Requirement: 独立服务端角色状态
模拟服务端 SHALL 将完整角色世界状态拆为资料与位置；选角及 RoleStateNotice SHALL 返回完整状态。MoveRt SHALL 只返回当前位置、地图、版本与移动确认序号。主动资料或位置提交成功后 SHALL 能分别发送 RoleProfileNotice 或 RolePositionNotice；普通移动 SHALL 不额外给自己发送位置推送。

#### Scenario: 延迟移动确认
- **WHEN** 选角成功后发送移动请求并推进模拟时间
- **THEN** 返回只含位置状态的 MoveRt，保持原有确认时机，且不产生重复的 RolePositionNotice

#### Scenario: 主动角色更新
- **WHEN** 服务端主动提交完整角色状态
- **THEN** 内存提交成功后发出包含资料和位置的 RoleStateNotice

#### Scenario: 主动局部更新
- **WHEN** 服务端分别提交当前角色资料或位置
- **THEN** 仅发出对应局部推送，另一部分状态保持不变
