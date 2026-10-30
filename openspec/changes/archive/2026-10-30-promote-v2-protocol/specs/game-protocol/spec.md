## Purpose
定义客户端和服务器共用的唯一正式游戏协议源、内部 Actor 协议边界与可读格式，使生成代码和实际业务始终使用同一套契约，消除旧协议兼容分支及无业务触发的通知。

## ADDED Requirements

### Requirement: 唯一正式协议
项目 SHALL 仅从 proto/ 生成正式游戏与内部 Actor 协议，客户端和服务器 SHALL 使用同一套契约。旧协议、旧服务器和双版本装配 SHALL 删除；现有业务、四字段信封、字段编号和当前 V2 存档 SHALL 保持可用。

#### Scenario: 正式链路验收
- **WHEN** 构建项目并完成登录、创角、进入、多人移动、物品操作与重启
- **THEN** 全部使用正式协议和服务器，已有 V2 存档继续读取，无旧协议类型参与执行

#### Scenario: 存档目录提升
- **WHEN** 首次启动且原 server/v2 含账号摘要、角色三表或地图目录
- **THEN** 全部提升到 server 根目录并保留原始数据，成功后移除空 v2 目录；遇到同名目标时不覆盖两侧数据

### Requirement: 协议格式
proto 消息、枚举及 oneof 的大括号与成员 SHALL 分行，字段 SHALL 使用两空格缩进，每两个定义之间 SHALL 空一行。格式调整 SHALL 保持既有字段编号和业务语义。

#### Scenario: 检查源文件
- **WHEN** 查看或重新生成全部 proto
- **THEN** 不出现单行消息体，定义间有一个空行，生成代码能够正常编译

### Requirement: 无触发通知暂不定义
正式协议 SHALL 不声明旧 RoleStateNotice、RoleProfileNotice、RoleStatusNotice、RoleChangedNotice；后续业务需要时独立定义所需更新，不为兼容旧测试预先增加推送。

#### Scenario: 当前角色状态
- **WHEN** 进入世界并进行现有移动、装备和物品操作
- **THEN** 初始快照、世界帧和物品组合覆盖所需展示，不依赖已移除的通知
