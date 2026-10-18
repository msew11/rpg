## ADDED Requirements

### Requirement: 启动加载全部客户端配置
客户端 SHALL 递归加载配置目录中的 JSON，以对应 JSON Schema 验证并组合成类型化游戏配置；未知、重复或无效配置 SHALL 给出文件相关错误。

#### Scenario: 加载地图配置
- **WHEN** 配置目录有合法的地图 JSON
- **THEN** 游戏配置包含该地图的尺寸、瓦片大小、默认地块和覆盖格

#### Scenario: 无效配置
- **WHEN** 配置目录包含未知类型、重复类型或不符合 Schema 的文件
- **THEN** 启动展示配置错误，不进入游戏世界

### Requirement: 大地图按可见范围呈现
地图管理类 SHALL 封装地图读取、坐标与边界，并仅绘制可见区域的瓦片。

#### Scenario: 千格地图
- **WHEN** 加载 1000×1000 地图并移动视口
- **THEN** 可见地块随视口更新，客户端不为每个格子创建常驻节点
