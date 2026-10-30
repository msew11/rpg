# Proposal

## Why

ServerModules 当前从外部接收 Login 处理器工厂，Role 和 World 则在构造方法内提供默认工厂，三类模块装配不一致。按用户要求将三类正式 Handler 全部在构造方法内注册，去掉处理器工厂参数。

## What Changes

- ServerModules 仅接收配置、仓库、诊断与共同保存依赖，三类处理器固定在自身构造方法内组装。
- MockServer 不再声明账号处理器表；保留用户已有注册简写与排版调整。
- 自定义处理器组合仅留在测试侧，宿主通过内部模块契约借用完整模块；正式业务仍使用同一个 ServerModules 实现。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。仅调整内部装配，不改变业务协议、存储或可观察行为，因此跳过 delta specs。

## Impact

涉及 ServerModules、宿主模块依赖和现有注入型底层测试；不改变玩法、正式服务生命周期或 Actor 协议。
