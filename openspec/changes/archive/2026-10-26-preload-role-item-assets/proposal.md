## Why

首次打开有物品的背包会同步解析约 7.39 MB、22,872 条记录的素材目录并加载图集。背包、装备、地面物品和图鉴分别建立目录索引，重复消耗进入游戏后的主线程时间。

## What Changes

- 客户端常驻 ItemAssetService 统一持有素材索引、按需图集与裁剪纹理，随整组服务释放。
- 选角成功后、世界及 HUD 创建前预热当前背包和装备的图标，不预加载全部图集。
- 四个素材消费者共用缓存；保留缺图占位与按需加载行为。
- 增加真实选角和首次开包测量、缓存复用及释放重建验收。

## Capabilities

### New Capabilities
- `client-item-assets`: 物品素材目录共享、按需纹理缓存与选角预热。

### Modified Capabilities
- `game-services`: 增加 Items 入口及对应固定构造和逆序释放约定。

## Impact

影响 GameServices、MainController、背包、装备、图鉴、地面物品表现和测试组装。更新架构及路线图；服务端角色存档、协议及玩法不变，不增加第三方依赖。
