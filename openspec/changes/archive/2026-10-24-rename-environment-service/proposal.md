## Why

按用户要求精简环境服务命名，并将其归入配置模块，使类型与目录职责一致。

## What Changes

- 将 EnvironmentService 重命名为 EnvService，迁移至 src/Config，命名空间使用 Rpg.Config。
- 同步构造、测试与当前文档引用，保留 GameServices.Environment 入口及目录、初始化和释放行为。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-services`：环境参数统一管理使用 Rpg.Config.EnvService。

## Impact

影响环境服务及其消费者、测试、架构文档和当前规范；不引入依赖或新的玩法决定。
