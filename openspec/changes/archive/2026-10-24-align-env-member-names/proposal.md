## Why

环境服务已更名为 EnvService，但全局属性、构造参数和测试辅助方法仍使用 Environment，导致同一服务存在两套命名。按用户补充要求同步这些成员，避免调用方继续沿用旧名称。

## What Changes

- 全局入口与注册属性 Environment 更名为 Env，参数与局部变量 environment 更名为 env。
- 同步测试辅助方法 CreateEnv、nextEnv 变量、反射断言以及当前文档和规范。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-services`：统一 Env 环境服务入口命名。

## Impact

影响 GameServices、配置/设置/网络构造、测试工厂与用例，以及架构文档和路线图。验收为当前代码无旧成员引用，项目检查与现有生命周期测试通过。
