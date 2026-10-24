# client-config-map Specification

## Purpose
TBD - created by archiving change data-driven-rpg-client. Update Purpose after archive.
## Requirements
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

### Requirement: 大地图按可见范围呈现
MapUtils SHALL 基于纯数据地图组件封装地图读取、坐标与边界计算；地图表现 SHALL 仅绘制视口附近的瓦片。静态地图表现 SHALL 复用已经绘制的可见分块，在可见分块范围变化时更新覆盖。每个分块 SHALL 将地表合为一个二维顶点颜色网格、网格线合为一次线段绘制，避免逐格提交绘制命令；分块离树时 SHALL 释放所拥有的网格。

#### Scenario: 千格地图
- **WHEN** 加载 1000×1000 地图并移动视口
- **THEN** 可见地块随视口更新，客户端不为每个格子创建常驻节点

#### Scenario: 静止与同范围移动
- **WHEN** 角色静止或移动但视口仍由相同的分块覆盖
- **THEN** 地图继续显示完整地块，已有分块不重新绘制

#### Scenario: 跨分块与窗口变化
- **WHEN** 摄像机跨越分块边界或窗口尺寸变化
- **THEN** 新进入视口范围的地块出现，仍需显示的分块被复用，离开的分块被释放

#### Scenario: 地图边缘
- **WHEN** 视口移动到地图边缘
- **THEN** 地图内的可见地块仍完整显示，地图外不创建地块

#### Scenario: 合并绘制保持画面
- **WHEN** 分块首次显示或相机跨界创建新分块
- **THEN** 合并绘制保持原有等轴位置、棋盘颜色、覆盖地块与网格线，静止时不重复构建几何
