## ADDED Requirements

### Requirement: 消息处理器单一职责与目录归属
服务端业务 Handler SHALL 每类只实现一个 IActorHandler 契约，类型和文件均以请求消息名加 Handler 命名，并独立存放在所属模块目录。跨 Actor 编排 SHALL 放在 Async 目录，只操作本地上下文的消息 SHALL 放在 Handler 目录；Handler 目录 SHALL 只存放 Handler。显式注册 SHALL 保持原消息与回复契约；跨 Actor 共用逻辑 SHALL 放在 Modules/Common/Utils，Actor 内共用逻辑 SHALL 放在 Modules/Xxx/Utils。

#### Scenario: 按消息查找独立实现
- **WHEN** 检查 Login、Role、World 的业务处理器与注册
- **THEN** 每类只有一个请求契约，类型与文件名对应请求名，注册仍覆盖原有消息

#### Scenario: 本地处理与跨 Actor 编排分开
- **WHEN** 检查角色物品命令、世界同步和账号转发
- **THEN** 本地 GM、背包移动、物品查询及拾取意图在 Handler，跨 Actor 转发、穿脱投影、丢弃与拾取完成在 Async；既有物品、移动及 AOI 行为保持一致

#### Scenario: 共用逻辑归属
- **WHEN** 多个 Handler 复用角色投影、物品规则、摘要或地图快照构建逻辑
- **THEN** 跨 Actor 投影发布位于 Common/Utils，模块内规则和数据投影位于各自模块 Utils，Handler 目录中没有共用辅助类型
