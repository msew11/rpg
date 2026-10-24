# 收敛现有服务实现约定

## Why
同类私有依赖与订阅集合有不同写法，设置保存还保留无独立职责的转发方法。仅通过删除和局部替换统一，避免为风格新增框架。

## What Changes
- MapManager 的私有配置依赖改为只读字段，与其他内部依赖一致。
- 设置保存统一直接调用已有 Write，删除 SaveSystem/SaveRole 单行转发；删除已由 CurrentRole 完成的重复检查。
- HUD 的事件订阅集合统一命名为 _subscriptions。
- 保留 JSON 容错、服务生命周期及依赖边界的实际差异，不新增类型或接口。

## Capabilities
### Modified Capabilities
- `game-services`: 明确内部收敛应复用既有实现，避免无职责转发和额外抽象。

## Impact
局部调整地图、设置与 HUD 内部代码，外部接口与运行行为保持一致。
