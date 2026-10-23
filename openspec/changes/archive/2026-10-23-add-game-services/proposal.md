# Proposal

## Why

运行代码目前需要从主控制器逐层传递 `NetService`。提供明确的全局服务入口，让客户端业务能直接访问网络方法，同时保留可替换传输、独立测试和可控的生命周期。

## What Changes

- 增加静态 `GameServices.Net` 只读入口，启动时显式注册网络服务；初始化前访问和重复注册明确报错。
- 初始化返回可幂等释放的所有权句柄，释放时清空入口并释放网络；允许随后重新初始化，旧句柄不影响新实例。
- `MainController` 注册、固定帧推进并释放服务，初始化失败时清理已获得的资源。
- `GameSession(LoginRt)` 从全局入口取得并绑定当前网络，生产代码不再传递网络构造参数；保留显式实例构造支持隔离测试。
- 覆盖全局访问、生命周期及场景重建回归，更新架构与路线图。

## Capabilities

### New Capabilities

- `game-services`: 客户端常驻服务的统一访问、显式注册及生命周期所有权。

### Modified Capabilities

无。现有网络字节、固定帧、会话隔离和缓存优先契约不变。

## Impact

新增 `src/Services/GameServices.cs`，调整 `MainController` 和 `GameSession`，扩充客户端单元与场景冒烟测试。不修改协议、存档和游戏规则；不将会话、世界或其他仓库全局化，不引入通用容器或自动创建服务。
