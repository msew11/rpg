# client-config-map Specification

## Purpose
TBD - created by archiving change data-driven-rpg-client. Update Purpose after archive.

## Requirements

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

### Requirement: 地图逻辑 chunk 调试边界

地图表现 SHALL 按当前地图配置的 `chunkSizeTiles` 绘制可见逻辑 chunk 的等轴细边框，在地图宽高处截断。绘制层 SHALL 覆盖地表且不影响地图静态分块复用或实体坐标。

#### Scenario: 视口移动到地图边缘

- **WHEN** GM 边界开启，视口跨越 chunk 或移动到地图边缘
- **THEN** 当前可见 chunk 的边框随视口更新，最后一个 chunk 的边框止于地图边缘

### Requirement: 地图背景音乐配置
地图配置 SHALL 提供可选的背景音乐曲目编号。地图 1 SHALL 配置为 M015a；配置编号 SHALL 是合法的曲目标识，未配置时该地图不播放地图 BGM。

#### Scenario: 地图 1 曲目
- **WHEN** 客户端加载项目自带的地图配置并读取地图 1
- **THEN** 地图 1 的背景音乐曲目编号为 M015a

#### Scenario: 未配置曲目
- **WHEN** 地图配置没有背景音乐曲目编号
- **THEN** 该地图仍可加载，进入该地图时不播放地图 BGM

#### Scenario: 无效编号
- **WHEN** 地图配置包含非法的背景音乐曲目编号
- **THEN** 启动时报告地图配置错误

### Requirement: 两层缩放下的静态地表绘制范围

静态地表 SHALL 按窗口尺寸、相机位置和滚轮基础缩放选择可见的 8×8 绘制块。GM 叠加缩放 SHALL 只改变最终显示比例，不扩大或缩小已选择的绘制块集合。GM 可显示实际绘制块整体区域的边界，边界 SHALL 随基础绘制范围更新并在地图边缘截断。

#### Scenario: GM 缩放观察未绘制区域

- **WHEN** 玩家保持相机位置、窗口尺寸和滚轮基础缩放不变，将 GM 倍率调小
- **THEN** 世界画面缩小并露出未绘制背景，地表绘制块集合保持不变

#### Scenario: 滚轮与地图边缘

- **WHEN** 玩家滚动鼠标改变基础缩放或移动到地图边缘
- **THEN** 地表绘制块集合按新的基础可见范围更新，已显示的绘制区域边界与实际块范围一致且不越过地图
