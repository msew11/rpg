## Why

当前 `simulation/` 与 `world/` 同时承载世界规则，地图和雷达的画面计算又注册为有状态 ECS System，目录与运行时职责不一致。统一边界后，世界逻辑、无状态工具和 UI 更容易辨认。

## What Changes

- 将世界状态、组件和真正更新 ECS 的系统整理到 `scripts/world/`；取消 `scripts/simulation/`。
- 在顶层 `scripts/utils/` 放置静态计算工具，统一使用 `Util` 类名前缀。
- 将区块范围更新改为无状态 ECS System；地图和雷达计算改为静态工具，UI 保留绘制及交互。
- 将场景入口 `main.gd` 和本地会话入口 `local_session.gd` 放在 `scripts/` 根目录。
- 保持已有移动、区块回环、运行期地块状态、雷达和地图行为。

## Capabilities

### New Capabilities

- `world-script-boundaries`: 规定本项目世界状态、ECS System、静态计算工具和 UI 的职责边界。

### Modified Capabilities

无。现有对外可观察的玩法要求不变。

## Impact

涉及 `scripts/simulation/`、`scripts/world/`、`scripts/view/`、`scripts/` 根目录、新增 `scripts/utils/`、相关测试和 `docs/architecture.md`。不修改 `scripts/core/`。
