## Why

地图逻辑只依赖传入数据，没有系统状态或 ECS 调度职责，静态 MapSystem 的命名与位置造成误解。保留纯函数实现，归入已有 Utils，使其职责与目录一致。

## What Changes

- 将静态 MapSystem 改名为 MapUtils，移入 World/Utils 并调整命名空间。
- 保留纯数据组件及所有计算行为，迁移服务、世界与测试调用。
- 更新架构与当前规格，明确纯计算工具与 ECS System 的边界。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ecs-world`: 地图数据构建和纯计算归 MapUtils，组件仅承载数据。
- `client-config-map`: 地图查询通过 MapUtils 完成，保持分块行为。

## Impact

影响地图工具、服务与 ECS 调用、相关测试及文档；无新增依赖、协议或玩法变化。
