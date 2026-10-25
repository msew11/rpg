## Why

移动确认频繁发生，现有 MoveRt 每次发送完整角色世界状态，重复传输不变的角色资料。拆分后可让频繁更新只携带位置，并为之后的局部角色状态更新建立清晰边界。

## What Changes

- 将角色世界状态拆为资料与位置两部分，选角和完整状态推送继续提供组合快照。
- 移动确认只返回位置；新增资料和位置的独立推送。
- 客户端分别按版本接纳两部分，世界仍从组合后的权威快照同步。

## Capabilities

### Modified Capabilities

- `mock-server`: 按更新类型发送完整或局部角色状态。
- `client-session`: 分别校验和合并角色资料及位置。

## Impact

更新 protobuf 消息结构、MockServer、RoleCache、世界同步和现有协议验收；不改角色 JSON 存档格式。
