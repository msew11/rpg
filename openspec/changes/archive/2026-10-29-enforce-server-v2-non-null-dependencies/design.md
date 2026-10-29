# Design

## Context

默认服务通过 null 表示待创建模块，ConfigService? 随测试入口进入 Module/Actor/Context，账号 Context 还允许缺失摘要。运行时的任务、失败、匿名身份和 Tell 回执则确实存在缺失状态。

## Goals / Non-Goals

用完整构造契约让必需依赖在内部始终非空，保留外部边界和实际可选状态。保持 ConfigService、本次未涉及的旧 Server、协议与存档模型不变。

## Decisions

- 默认 MockServerService 构造先装配完整 ServerModules；内部注入构造只接收 EnvService 和非空 ServerModules，配置直接来自模块，消除双传配置不一致的可能。
- ServerModules 构造必须提供有效 ConfigService、仓库与登录工厂。可选角色/地图工厂和诊断回调只在装配入口补齐，闭包及分片不再判空。ActorDirectory 同样在公开构造边界补齐默认诊断回调。
- Actor 和 Context 必须接收非空 ConfigService；LoginContext 必须接收完整摘要，不再兜底构造空集合。Config 属性只检查借用生命周期。ModuleContext 的可扩展构造边界拒绝 null；内部 Actor 不重复检查已确定的依赖。
- 路由当前所有成功结果都有账号绑定键，DataResponse.BindUsername 明确非空；错误输出的可选绑定、会话和生命周期空值保持。
- 使用真实配置的模块测试归 Godot 测试宿主，通用邮箱与文件仓库测试仍独立运行；禁止以 null!、未初始化对象或测试专用生产构造保留配置漏洞。
- 逐项记录保留的 nullable：匿名身份、无效代次、任务未启动、失败尚未发生、Tell 无回执、屏障无数据、回滚原文件缺失、诊断订阅及接口比较器的空值契约。JSON 根 null 和公开消息入口参数仍在边界检查。

## Risks / Trade-offs

- 模块测试依赖真实配置加载 → 在既有 Godot 宿主执行并保留原断言和全部验收场景，不改生产配置加载逻辑。
- 调整构造影响调用点 → 编译并完整运行 V2、普通 .NET、Godot 和原客户端场景；入口测试验证错误早于分片与 IO。

## Migration Plan

同步更新构造调用与测试装配，文档明确入口和内部契约；无协议及存档迁移。可回滚整个提交。
