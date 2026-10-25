## Context
锚点开关目前只通过 GameHud 事件更新 ActorView 和 GroundItemsView；EnvService 已提供复用的 .env 单键保存逻辑。

## Decisions
由 EnvService 持有布尔状态，键名为 `RPG_SHOW_ENTITY_ANCHORS`，使用与日志键相同的读取、保存和失败保留语义。GM 控件从 Env 初始化，保存成功后发送现有事件；保存失败恢复原值。MainController 建立世界并订阅 HUD 事件后，直接把 Env 状态应用到当前角色与地面物品表现。颜色继续默认黄色，仅在当前世界即时调整，不另行持久化。

## Validation
覆盖默认、合法与非法值读取，开关的跨实例记忆、其他键保留和写入失败；场景验收覆盖即时显示、关闭及重启后恢复。
