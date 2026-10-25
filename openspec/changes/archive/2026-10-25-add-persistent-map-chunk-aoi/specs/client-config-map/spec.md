# Spec Delta

## MODIFIED Requirements

### Requirement: 启动加载全部客户端配置
客户端 SHALL 统一通过 Godot 的 DirAccess 和 FileAccess 读取平铺于配置根目录的 JSON 并拒绝子目录，不为独立 .NET 测试保留 System.IO 加载分支；配置加载及依赖该加载的验收 SHALL 在 Godot headless 环境执行。客户端 SHALL 加载全部 JSON，以对应 JSON Schema 验证并组合成类型化游戏配置；地图配置文件 SHALL 命名为 `map.json`。地图 SHALL 使用标注 map.json 的 MapCfgCache : MultiCfgCache<int, MapCfg>，以根数组承载多地图配置且不含 kind；每行 SHALL 包含正整数主键 id 和正整数 chunkSizeTiles，basic.json 的 initialMapId SHALL 引用存在的地图；未知、重复或无效配置 SHALL 给出文件相关错误。

#### Scenario: 加载地图配置
- **WHEN** 配置目录有合法的 `map.json`
- **THEN** 游戏配置包含该地图的尺寸、瓦片大小、默认地块和覆盖格

#### Scenario: 无效配置
- **WHEN** 配置目录包含未注册文件名、配置子目录或不符合 Schema 的文件
- **THEN** 启动展示配置错误，不进入游戏世界

#### Scenario: 文件名与类型不对应
- **WHEN** 地图配置文件名不是 MapCfgCache 标注的 `map.json`
- **THEN** 加载失败并指出该文件

#### Scenario: 多地图引用与初始化
- **WHEN** 地图表包含不同数值 ID 且角色进入其中一张地图
- **THEN** 按角色地图 ID 初始化世界，不依赖表中顺序，离开世界后清理当前地图

#### Scenario: 初始地图不存在或身份重复
- **WHEN** initialMapId 不存在、地图 ID 重复或分块尺寸无效
- **THEN** 启动报出相关配置错误，不进入世界
