## Why

用户进入游戏移动时，Rider Monitoring 报告多次 UI freeze 和原生绑定调用耗时。需要区分真实帧停顿、首次资源加载及监控判定，并以可重复测量确定优化对象。

## What Changes

- 增加隔离存档的真实游戏移动诊断场景，记录进入、静止、连续移动的帧时间、引擎处理时间与分配量。
- 根据实测修复已确认的移动表现热点，保持移动、外观及持久化契约。
- 保存测量结果和诊断限制，运行现有回归检查。

## Capabilities

### New Capabilities
- `movement-performance`: 可重复的 Windows 移动性能诊断。

### Modified Capabilities
- `client-config-map`: 地图分块合并地表及网格线绘制，保留分块覆盖与复用行为。

## Impact

影响测试诊断及经测量确认的表现层热点，不引入玩法或美术决策。
