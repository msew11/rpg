## Why

无参入口仍把服务创建委托给 Register 和注册句柄构造，无法在一个方法内读出完整初始化顺序。按用户要求将正式服务创建全部集中到 Initialize。

## What Changes

- Initialize 直接创建全部八个服务，显式表达依赖顺序。
- Register 只验证并注册现有实例，注册句柄只持有实例与清理。
- 隔离测试自行组装服务，不在正式注册代码中保留测试工厂。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-services`：完整创建顺序集中于无参 Initialize，注册不再隐式创建服务。

## Impact

GameServices、隔离测试组装、架构和路线图；正式公开调用方式与业务行为保持一致。
