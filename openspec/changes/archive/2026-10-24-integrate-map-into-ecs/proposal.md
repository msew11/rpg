## Why

DataMapComponent 目前承载带有配置服务依赖和查询方法的 MapManager，违背 Component 只保存数据的职责。将地图数据和行为归入现有组件与系统目录，使地图进入既有 ECS 框架。

## What Changes

- 地图组件保存只读尺寸、默认地块和稀疏索引，不持有管理器、服务或业务方法。
- 新增无状态 MapSystem 处理配置到数据的转换、坐标裁剪、地块及可见范围查询，删除 MapManager。
- 世界初始化 Action 只携带地图数据，服务和 ECS 共用相同计算规则。
- 保持点击移动、权威修正和地图分块表现行为；同步文档并覆盖回归风险。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `client-ecs-world`: 约束组件仅保存数据，地图行为归入系统。
- `client-config-map`: 地图计算由 MapSystem 实现，保持原有可见范围与分块要求。

## Impact

影响 src/World、地图相关验收与冒烟测试、架构及路线图。无新增依赖，无协议或存档变更。
