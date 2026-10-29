# Proposal

## Why

手写 C# 中残留未使用的 using 和可省略的命名空间限定，Rider 将其显示为灰色，增加阅读噪声。

## What Changes

- 清理正式代码和测试代码中语义分析确认冗余的 using 与名称限定。
- 保留必要的消歧限定、生成代码和既有行为。
- 验收：截图中的冗余项消失，同类诊断复查通过，项目检查及既有测试通过。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

无。纯代码整理，设置 `skip_specs: true`；继续遵循现有服务端处理、物品丢弃及其他功能契约。

## Impact

影响解决方案中的手写 C# 引用与类型名称写法，不改变 API、协议、依赖、玩法或模块边界。
