## Context

ConfigService 的 LoadGodot 和 System.IO Load 共用校验，但独立 .NET 测试使用后者。配置测试及 GameServicesTests 会构造配置服务；部分世界计算测试也不必要地读取正式配置。

## Goals / Non-Goals

**Goals:** 正式配置加载只有 Godot API 一条路径；保留配置错误、服务生命周期等已有验收；测试适配运行环境。

**Non-Goals:** 新增文件系统抽象、解析器接口、测试用构造或业务分支。

## Decisions

- 构造始终调用 Load，原 LoadGodot 改名为 Load，删除 System.IO 读取和路径前缀判断。保留现有扫描、JSON 解析与 Schema 校验。
- 依赖配置服务的现有用例标记 Runtime=Godot，在真实 Godot headless 进程内使用已有 xUnit 运行器执行。通过测试场景加载独立测试程序集，生产项目不新增 xUnit 依赖。
- 普通 .NET 运行使用测试过滤器排除该标记；提供 tools/test.ps1 顺序执行普通用例与 Godot 用例。纯地图计算用例直接构造 MapConfig。配置错误测试仍可创建临时目录，由相同 Godot API 读取，不维护第二套实现。

## Risks / Trade-offs

测试程序集依赖位于独立输出目录 → 测试场景仅在运行测试时解析该目录的依赖，失败通过退出码报告，缺少测试程序集时明确报错。Godot 配置读写需要原生环境 → 仅带相应标记的用例移入引擎，保留现有断言。
