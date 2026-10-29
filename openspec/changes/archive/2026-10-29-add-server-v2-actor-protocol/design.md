# Design

## Context

当前 develop 1f20cec 已提供 Ask<TReply>(object)、按 CLR 类型注册的 ActorHandlers，以及完整 World INIT/分块写回。Context 仅持有配置，默认 Role/World 注册表为空。动机见 proposal.md。

## Goals / Non-Goals

**Goals:** 保持 Runtime 的通用性，以实际 Role→World 只读调用验证内部协议与模块定位边界。

**Non-Goals:** 不增加游戏规则、认证、通知框架、死锁检测、跨 Actor 事务或客户端入口切换，不改变存档格式。

## Decisions

- 用两个明确接收方协议文件：world.proto 声明 GetWorldObjectCount/GetWorldObjectCountRt(count int32)，role.proto 声明 GetRoleWorldObjectCount/GetRoleWorldObjectCountRt(count int32)。独立 Role 入口避免把 World 接收方协议注册到 Role；不预建空 login/common 文件。
- 外部 Protobuf 通配排除 actor/**/*.proto；内部 ProtoRoot=protov2/actor、OutputDir=src/ServerV2/ActorProtocol/Generated、GrpcServices=None，Compile 排除默认通配后由 Grpc.Tools 纳入。生成 C# 与 Godot UID 由工具产生并提交。
- public IActorLocator 位于 Modules/Common，仅返回 ActorRef。ServerModules 实现接口，三类 Actor 必需构造传入，再交给 ModuleContext 的只读 Actors 属性。目录工厂捕获 this 是安全的：Get 时才执行；不使用全局定位、属性回填或可空捷径。
- 默认 Role 注册 Async/GetRoleWorldObjectCountHandler，World 注册 Handler/GetWorldObjectCountHandler；显式注入的注册工厂完全由调用者控制。所有处理器共用 IActorHandler/ActorHandlers，无逐请求绑定层。
- Role Handler await context.Actors.World(context.Role.MapId).Ask<GetWorldObjectCountRt>(new GetWorldObjectCount())，映射到 Role 回复；World 返回 context.Objects.Length。DispatchAsync 继续仅保留现有 DataFlush 存储边界。
- 直接移交可变 proto 的独占使用权，禁止请求入队后修改及返回回复后留存修改。Data 持久化模型独立；内部协议不进入 MessageCodec。

## Risks / Trade-offs

- [自身 Ask 或循环等待会阻塞邮箱] → 在文档明确禁止，不增加检测系统；验收覆盖正常单向调用和错误恢复。
- [proto 可变，依赖使用者遵守所有权约定] → Handler 不保存请求/回复，测试验证原实例直达和无额外转换。
- [构造签名影响既有测试] → 更新全部 V2 构造点，使用完整服务模块或真实 fixture，不减少配置契约验收。

## Migration Plan

按协议生成、构造注入、处理器、验收及文档顺序实施。执行 tools/check.ps1 -Tests，归档规格后独立本地提交。无存档迁移；回滚此提交即可移除内部能力。
