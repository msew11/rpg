# Design

## Context

当前 Login 协议经路由转换为 DataLogin，处理器返回 DataLoginResult，再由路由构造 LoginRt。运行时请求泛型约束要求实现 IActorRequest，生成的协议没有该接口。持久化模型与协议不同，继续保留。

## Goals / Non-Goals

直接注册协议请求与回复，不修改生成代码；保留内部 Flush 等请求的类型推断。登录认证、存储格式与会话语义不属于此次变更。

## Decisions

- 移除 IActorHandler 和请求注册的 IActorRequest 约束；ActorRef 增加 Ask<TReply>(object) 入口，保留内部 IActorRequest 重载。BaseActor 统一接纳对象并检查回复类型。运行时不依赖 protobuf，不为生成类型增加 partial 接口或包装消息。
- LoginHandler 使用 ProtocolV2.Login/LoginRt，路由规范化账号键定位分片，直接投递原 Login、返回原 LoginRt。连接层仍只在成功且代次有效时绑定身份。删除 DataLogin.cs 及 uid。
- protobuf 对象可变但每次请求独占：解码后移交邮箱，处理器返回后移交响应，双方不再修改已移交对象。内部状态快照仍不可变，持久化 Data 类型继续用于 Context；组装协议响应是 Handler 的职责。

## Risks / Trade-offs

- 显式回复泛型与请求不匹配 → 保留运行时回复类型检查并测试错误后邮箱仍可用。
- 共享可变协议对象会破坏串行边界 → 文档明确独占约定，测试原实例直达并验证字段保留。

## Migration Plan

同步调整装配、路由和测试处理器，无存档或网络协议迁移。删除公开 DTO 属于源码兼容性变化，提交标注 BREAKING CHANGE；回滚提交可恢复旧接口。
