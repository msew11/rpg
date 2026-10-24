## Why

选角页仅能进入两个固定演示角色，无法创建、删除或选择外观。需要支持完整角色管理，并将角色协议统一归入 role.proto。

## What Changes

- 迁移角色摘要、世界状态及 SelectRole/SelectRoleRt 到 role.proto，保留包名、消息名和字段编号。
- 新增创建与删除协议、服务端持久化和缓存优先的列表刷新；首次初始化仍提供两个演示角色。
- 创建页填写名字、选择性别与全部 148 套头型（男女各 74），预览下方按钮每次顺/逆时针旋转 45°。
- 头型贯通摘要、存档、世界与全部基础动作；旧存档默认使用头型 1。
- 删除前确认，允许空列表，重启不恢复已删除角色。

## Capabilities

### New Capabilities
- `role-management`: 创建、删除、头型选择及预览的完整行为。

### Modified Capabilities
- `client-network`: 角色协议归属与新增请求响应。
- `client-session`: 动态角色列表、头型数据及缓存更新。
- `mock-server`: 动态角色集合和持久化删除。
- `client-actor-presentation`: 全头型、全部动作的表现。

## Impact

涉及 proto 与生成代码、Session、MockServer/Repo、ECS 快照、动画资源与导入工具、选角 UI 和验收测试。不增加服务或真实服务器连接，不修改 main 专属文件。
