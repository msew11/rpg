## Context

事件服务当前位于 src/Services/EventService.cs，网络通过构造绑定，缓存与 UI 从 GameServices.Events 访问。

## Goals / Non-Goals

目标：采用 EventBusService、EventBus、eventBus、_eventBus 的一致命名，保持全部事件行为。
非目标：改变事件阶段、错误隔离、重入、订阅边界与初始化及释放顺序。

## Decisions

在原目录更名脚本并保留原 UID，直接同步所有消费者，不增加旧名转发。测试辅助 EventServiceTestScope 同步为 EventBusServiceTestScope，反射类型和释放顺序断言使用新名称。当前文档和规范同步，历史归档保留。复用现有事件和生命周期验收，不添加重复测试。

## Risks / Trade-offs

遗漏反射字符串或测试基类名可能导致构建或验收失败；以全局扫描、完整项目检查与现有测试验证。
