## Why

启动入口逐层传递目录和网络工厂，环境参数散落在控制器和服务构造中。将环境统一为一个服务，让正式启动无参、依赖顺序清晰，减少组装责任。

## What Changes

- **BREAKING**：正式入口改为 `GameServices.Initialize()`，控制器不再提供目录或网络工厂。
- 增加 `EnvironmentService`，统一配置、Schema、设置与模拟服务端目录。
- 正式服务构造只接收服务依赖；保持显式构造和逆依赖清理，不引入容器或自动排序。
- 隔离测试使用内部注册和环境覆盖边界，保持业务行为及异常清理。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-services`：无参启动、统一环境入口和按依赖构造及释放。
- `session-events`：明确事件在消费者之后、环境之前释放。

## Impact

涉及 GameServices、ConfigService、SettingService、NetService、MainController，以及现有服务和场景验收。更新架构与路线图；不改变玩法、存档格式或网络协议。
