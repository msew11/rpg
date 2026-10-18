## Why

当前项目只有空白主场景，需要一条可运行的客户端纵切流程来验证数据驱动、会话隔离和 Godot 表现层边界，为后续接入服务器与具体玩法提供基础。

## What Changes

- 启动时校验并加载 JSON 配置，生成组合式游戏配置；以稀疏地块配置展示 1000×1000 地图。
- 加入模拟登录、角色选择、会话及会话内数据缓存。
- 加入由 Godot 固定帧驱动的轻量 ECS 世界，所有外部输入与状态更新通过 Action 进入世界，支持点击移动、改目的地及模拟权威修正。
- 加入独立于 ECS 世界的 HUD、基础面板和小地图。
- 加入按系统与角色分别持久化的本地设置、设置页签及 HUD 调整。

## Capabilities

### New Capabilities

- `client-config-map`: JSON 配置校验、组合与大地图加载表现。
- `client-session`: 模拟登录、选角、会话与数据缓存。
- `client-ecs-world`: Action 驱动的 ECS 初始化、移动与权威校正。
- `client-ui`: HUD、菜单面板、小地图和配置/缓存数据呈现。
- `client-settings`: 系统及角色设置持久化与 HUD 调整。

### Modified Capabilities

无。

## Impact

新增 C# 客户端代码、JSON Schema 与示例配置、Godot 场景和 C# 验收测试；更新 `develop` 的愿景、架构和路线图。主场景仍为 `scenes/main.tscn`，首期不接真实服务器。
