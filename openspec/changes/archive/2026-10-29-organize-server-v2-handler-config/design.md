# Design

## Context

当前只有 LoginHandler 是正式业务处理器，直接在 Login 模块根目录且构造持有 ConfigService。三个 Context 尚无配置入口。纯 .NET 框架测试不依赖需要 Godot 加载的 ConfigService。

## Goals / Non-Goals

统一处理器目录和配置访问，保持协议入口及存档生命周期。不实现登录业务，不改变 ConfigService 的加载、缓存和所有权。

## Decisions

- 业务处理器放入 `Modules/<模块>/Handler`，命名空间同步。运行时处理契约与测试替身留在各自位置。
- `Modules/Common/ModuleContext` 提供只读 `Config` 属性，LoginContext、RoleContext、WorldContext 继承它。服务经 ServerModules、Actor 构造把同一 ConfigService 传至 Context；Handler 通过 `context.Config.Basic.Value` 等现有 API 读取。不采用静态入口，避免多服务串用和隐藏生命周期。
- 纯内存测试允许内部构造省略配置，但读取 Config 时明确抛错。正式 MockServerService 装配必须带配置；内部注入模块也校验配置实例与服务一致。Context 借用配置，不负责释放，读取时检查已释放状态。
- LoginHandler 不保留任何配置字段或构造参数；占位登录没有配置业务需要，继续返回 NotImplemented。

## Risks / Trade-offs

- 上下文公开 ConfigService 允许拿到服务 → 文档规定只读业务使用，生命周期仍由原所有者管理，不新增服务注册器。
- 无配置的纯 .NET 测试误读配置 → 属性明确拒绝，生产装配与多实例隔离在真实 Godot 测试中验证。

## Migration Plan

移动 Handler 与 uid，调整命名空间及装配。Context 只增加 API，存档和线上协议不变；回滚本提交即可恢复原目录与依赖方式。
