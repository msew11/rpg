# 收拢服务释放状态

## Why
七个服务各自声明 _disposed，并重复声明状态访问及存活检查。接口只能声明状态契约，不能保存实例字段，需用薄基类共享实现。

## What Changes
- IService 声明只读 IsDisposed。
- Service 基类保存 IsDisposed 并提供受保护的 ThrowIfDisposed；具体服务仍实现 Dispose。
- 七个服务移除自己的状态字段和重复检查方法，不改各自清理顺序。
- 本次是用户明确要求的共享状态实现；此前禁止仅为写法增加抽象的约束仍适用于纯风格修改。

## Capabilities
### Modified Capabilities
- `game-services`: 共享释放状态及状态检查，保留服务清理职责。

## Impact
服务类继承 Service，状态可由 IService 只读查询；不调整注册机制、协议或游戏行为。非服务的缓存、传输和注册句柄不纳入基类。
