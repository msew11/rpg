# Proposal

## Why

角色当前没有性别数据，玩家 entity 使用圆形占位表现。已准备的男女基础身体动画需要通过角色数据和 ECS 状态接入，并保持逻辑与资源播放分离。

## What Changes

- 角色数据增加男性、女性属性，演示角色分别覆盖两种性别。
- 玩家 entity 保存性别、动作与逻辑朝向，通过只读快照提供给表现层。
- 接入男女各 13 个动作的全部帧和定位点，按八方向播放；点击移动切换跑步，到达后站立并保留朝向。
- 提供经 Action 更新非移动动作的接口，不新增战斗、技能、坐骑或换装规则。

## Capabilities

### New Capabilities

- `client-actor-presentation`: 基于角色性别及 ECS 动作、朝向展示基础身体资源。

### Modified Capabilities

- `client-session`: 登录、选角和缓存保留角色性别。

## Impact

影响会话数据、ECS 组件与系统、ActorView、主场景协调器、资源导入及验收测试。资源复制到项目内，运行和导出不依赖外部 art_export 目录。不处理独立头部、发型、装备和坐骑资源。
