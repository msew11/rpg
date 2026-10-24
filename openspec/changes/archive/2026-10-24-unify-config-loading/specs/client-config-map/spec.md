## MODIFIED Requirements

### Requirement: 启动加载全部客户端配置
客户端 SHALL 统一通过 Godot 的 DirAccess 和 FileAccess 递归读取配置目录中的 JSON，不为独立 .NET 测试保留 System.IO 加载分支；配置加载及依赖该加载的验收 SHALL 在 Godot headless 环境执行。客户端 SHALL 加载全部 JSON，以对应 JSON Schema 验证并组合成类型化游戏配置；地图配置文件 SHALL 命名为 `map.json`。未知、重复或无效配置 SHALL 给出文件相关错误。

#### Scenario: 加载地图配置
- **WHEN** 配置目录有合法的 `map.json`
- **THEN** 游戏配置包含该地图的尺寸、瓦片大小、默认地块和覆盖格

#### Scenario: 无效配置
- **WHEN** 配置目录包含未知类型、重复类型或不符合 Schema 的文件
- **THEN** 启动展示配置错误，不进入游戏世界

#### Scenario: 文件名与类型不对应
- **WHEN** 地图配置的 `kind` 为 `map`，但文件名不是 `map.json`
- **THEN** 加载失败并指出该文件
