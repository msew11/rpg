# Proposal

## Why

V2 配置和模块本应在装配时完整确定，目前为测试保留的可空依赖却一直传到 Actor 和 Context。缺失依赖应在入口被拒绝，内部不再反复兜底。

## What Changes

- 默认服务先创建完整模块，注入构造只接收非空模块并从模块取得同一配置。
- 配置贯穿模块、Actor、Context 为必需非空依赖，账号 Context 必须接收完整摘要。
- 可选处理器工厂和诊断回调在装配入口一次补齐，内部直接调用。
- 检查全部 V2 可空用途，保留真实缺失状态、外部数据边界与必要生命周期检查。
- 模块集成测试使用真实配置并在 Godot 执行，通用邮箱及文件仓库测试保持普通 .NET 可运行。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `server-v2-account`：必需依赖在装配入口完整确定，缺失或已释放配置在创建分片和存储操作前拒绝。

## Impact

影响 V2 服务和模块构造、Context、诊断绑定与测试装配；不改变协议、存档格式、匿名会话含义或旧 Server。ConfigService 的加载实现保持不变。
