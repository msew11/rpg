## Context

环境服务位于 Services，配置、设置、网络及验收测试均直接引用该类型。

## Goals / Non-Goals

目标：类型更名为 EnvService 并放在 Config 中，所有引用可编译且现有生命周期验收通过。
非目标：调整 GameServices.Environment 入口、运行行为或服务构造顺序。

## Decisions

采用 Rpg.Config 命名空间并移动原有 .uid，遵循按功能组织目录的约定；直接更新消费者，不保留旧类型包装。当前规范与架构文档同步，已有归档保留历史记录。复用现有验收，不为机械重命名添加重复测试。

## Risks / Trade-offs

遗漏命名空间或资源引用可能导致构建或导入失败，通过全局引用扫描与项目检查、现有测试验证。
