## Context

现有入口接收设置目录、服务端目录和网络工厂，ConfigService 自行硬编码资源路径。七个业务服务已有 Service 生命周期及固定注册句柄，测试另有内部实例注册。

## Goals / Non-Goals

**Goals:** 无参正式启动；环境参数集中；公开构造只依赖服务；显式顺序与逆依赖释放；保留隔离验收。

**Non-Goals:** 自动依赖注入、服务发现、反射、玩法与存档格式变更。

## Decisions

- EnvironmentService 是无依赖根服务，默认持有 res://config、res://schemas、user://settings 与 user://server 的路径。设置及服务端目录在内部转为绝对路径；环境测试构造允许覆盖目录，不调用 Godot。
- ConfigService、SettingService、NetService 的正式构造接收 EnvironmentService。配置按环境路径保留 Godot 资源包读取和普通目录读取，兼容导出与独立测试。
- GameServices.Initialize() 按 Environment、Config、Setting、Net 及事件、Session、World、Animation 的依赖顺序组装。Environment 最后释放；失败清理已成功构造的服务，重复初始化在创建前拒绝。
- 既有显式实例注册改名为内部 Register，并明确接收环境。网络传输和已有登录响应构造仅为内部测试边界；不把工厂或测试传输塞入环境。控制器只保留可覆盖的整组初始化方法，测试替身负责隔离目录及传输，正式控制器不再保留环境字段。
- 使用现有固定注册与清理逻辑，不采用容器或通用构建框架，保持依赖可直接阅读。

## Risks / Trade-offs

- 资源路径与磁盘路径读取方式不同 → 保留资源包读取，配置与 Schema 都取自同一环境。
- 失败和重启可能留下环境或事件入口 → 回归覆盖失败清理、重复初始化及旧句柄释放。
- 内部测试边界不受公开构造限制 → 使用可见性明确隔离，运行路径只接收服务。
