## MODIFIED Requirements

### Requirement: 内部协议独立生成
内部协议 SHALL 以 proto3 按接收方模块组织在 protov2/actor，使用 rpg.v2.actor package 与 Rpg.ServerV2.ActorProtocol 命名空间，生成到 src/ServerV2/ActorProtocol/Generated。请求 SHALL 使用动作名，响应追加 Rt，结构和枚举使用 Pb；文件依次组织请求响应、推送、枚举结构，无内容部分省略。内部协议 SHALL 使用 protov2 作为 ProtoRoot，以 actor/role.proto 等逻辑路径生成；允许导入公共角色、背包与地图结构，公共生成规则 SHALL 排除内部目录，共享类型仅生成编译一次。

#### Scenario: 构建内外协议
- **WHEN** 构建包含 world.proto 与 role.proto 的项目
- **THEN** 内部完整消息名以 rpg.v2.actor 开头，生成类型只出现在内部目录且无重复编译，外部协议保留原命名空间
