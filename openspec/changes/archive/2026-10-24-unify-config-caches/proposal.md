## Why

地图与道具配置使用不同包装和加载分支，新增配置需要修改 ConfigService 的解析逻辑。统一单例和多例缓存，让文件格式、主键和加载责任具有明确约定。

## What Changes

- 配置文件平铺于 config/，拒绝子目录。
- 单例 JSON 根为对象，多例根为对象数组，移除 kind 与 items 包装。
- 新增 SingleCfgCache<TCfg>、MultiCfgCache<TKey, TCfg>，具体 XxxCfgCache 标注 CfgFile，行属性标注 CfgKey。
- ConfigService 只持有并加载 MapCfgCache 与 ItemCfgCache，统一缺文件、重复文件、Schema 和主键错误异常。
- **BREAKING** MapConfig 更名 MapCfg；入口改为 Config.Map.Value 与 Config.Item.Items，迁移全部调用与测试。

## Capabilities

### New Capabilities
- `client-config-cache`: 单例/多例缓存、文件标注、主键标注、统一加载和生命周期。

### Modified Capabilities
- `client-config-map`: 用文件名匹配单例 MapCfgCache，移除 kind 分派。
- `client-item-config`: 使用根数组与 ItemCfgCache，主键来自标注。

## Impact

影响配置代码、JSON/Schema、地图消费者、测试及架构/路线图。保留 Godot 读取链路、地图内容和空道具表，不改设置存档或 GM 图鉴格式，不引入新依赖。
