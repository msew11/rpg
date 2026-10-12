# Proposal

## Why

当前区块仅停止绘制，全部地块仍常驻内存；存档流程也假设能一次读取全部区块。需要让配置决定活跃范围，按玩家位置真正加载与卸载区块，并让卸载状态经会话临时存档进入五个正式槽位。写入失败须在游戏内 Chat 面板以系统消息提示。

## What Changes

- 配置单区块 tile 尺寸和活跃区块范围；首次按玩家坐标加载，移动后增量加载与卸载。
- 世界组件仅持有活跃区块地块数据；世界实体管理区块实体，玩家直接归属世界实体，区块可管理其子实体。
- 组件提供落地字符串与还原入口；实体区分落地后卸载和直接摧毁。
- 会话使用临时存档保存卸载的增量，读档先复制正式槽位，点击保存时合并临时存档与活跃缓存。
- 完整地图依据尺寸和玩家坐标绘制线框，不要求远处地块常驻。
- 新增 Chat 面板，临时存档写入失败时发出系统消息，仍完成内存卸载。

## Capabilities

### New Capabilities

- `system-chat`: 在游戏内显示系统消息及存档错误。
- `session-working-save`: 管理会话临时存档、区块增量与正式槽位提交。
- `entity-persistence`: 定义组件的双向落地契约和实体卸载、直接摧毁的行为。

### Modified Capabilities

- `continuous-chunk-world`: 可配置活跃范围、真正的区块数据卸载与重载，并调整完整地图的数据依赖。
- `start-menu-and-save-slots`: 通过会话工作副本恢复和保存五个正式槽位。

## Impact

影响 `scripts/world/`、`scripts/data/`、`scripts/local_session.gd`、`scripts/save_slots.gd`、`scripts/view/`、`scenes/main.tscn` 及对应测试。通用 ECS 父子实体与级联销毁由 `main` 上独立 change 实现，再合并到 `develop` 使用。
