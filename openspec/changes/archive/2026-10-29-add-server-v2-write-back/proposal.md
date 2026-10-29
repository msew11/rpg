# V2 Actor 数据加载与写回框架

## Why

V2 的 LoginActor、RoleActor 目前只有空 Context，无法从文件恢复状态或保存后续内存修改。需要在独占状态与串行邮箱边界内建立可重试、可恢复的读写能力，为后续业务提供统一入口。

## What Changes

- INIT 加载账号摘要和角色三表，Context 持有不可变快照、保存基线与待写标记。
- Actor 在邮箱内处理 Flush；仓库承担文件 IO、原子替换和角色多表回滚恢复。
- MockServerService 注入 EnvService，使用 server/v2，提供周期、显式及关闭保存。
- 保留默认登录占位、旧客户端入口和旧存档；不增加业务数据校验或跨 Actor 事务。

## Capabilities

### New Capabilities

- `server-v2-storage`: V2 账号及角色状态的初始化、修改跟踪、文件提交和写回调度。

### Modified Capabilities

- `server-v2-account`: 账号摘要由接口契约推进至真实文件读写，入口增加环境服务依赖。

## Impact

影响 src/ServerV2、V2 Net 适配及对应测试。MockServerService 公开构造改为接收 EnvService 与 ConfigService；不修改 protov2 或旧 Server。
