# Design
## Context
现有 MockServerService 在构造时组装 ServerRuntime，Net 默认旧传输，V2 由本地对象是否存在判断。
## Goals / Non-Goals
Goals：登录时拥有服务器、真实异步 TCP、保持固定帧事件和既有保存顺序。
Non-Goals：本 change 不实现移动、自动重连或新增会话业务字段。
## Decisions
- MockServer 提取原服务业务，Service 只保存借用依赖与可空实例。测试显式组装进程内服务器。
- TcpServerHost 管理监听与每连接收发，ConnectionSession 保存有界输出并提供异步可读信号；MockServer 自带后台周期调度。
- TcpClientTransport 后台接收、记录响应完成，Tick 才交付；本地写回等待后台响应屏障后 Flush。
- Net 使用显式 IsV2/能力，旧测试调用显式 Legacy 工厂；登录 UI 提供模式、IP、端口以及运行配置变更确认。
## Risks / Trade-offs
- 主线程同步写回等待 TCP：响应屏障只依赖后台接收，不依赖事件消费。
- 监听失败：候选实例尚未交给 Service，失败时完整释放。
