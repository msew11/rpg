## Why

用户确认使用 EventBusService 表达事件总线职责，并要求同步修改。现有 EventService、Events 和 events 命名需要统一，确保类型、入口与依赖参数表达一致且调用清晰。

## What Changes

- EventService 更名为 EventBusService，脚本与 UID 同步更名。
- 全局入口与注册属性改为 EventBus，参数、局部变量与字段分别改为 eventBus、_eventBus。
- 同步测试辅助类型、反射断言、当前规范和文档。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-services`：事件服务类型与全局入口改为 EventBusService、EventBus。
- `session-events`：订阅、发布与监听错误统一通过 GameServices.EventBus 访问。

## Impact

影响服务注册与网络依赖、缓存和 UI 调用、测试辅助类型及事件验收。验收为旧类型和成员引用清除，构建、Godot 导入与主场景启动正常，现有事件分发与生命周期测试通过。
