# Proposal

## Why

NetService 的发布和重置仍通过 GameServices 获取事件服务，SessionService 的内部构造仍获取全局网络，违反已确认的构造注入边界。需要使释放只依赖创建时绑定的实例，消除事件入口为清理保留独立状态的原因。

## What Changes

- 服务只通过构造接收服务依赖，禁止服务内访问 GameServices；禁止循环、属性回填及延迟解析。
- 固定 Environment、Events、Config、Setting、Net、Session、World、Animation 的构造顺序，严格逆序释放，逐项异常不阻断后续释放。
- NetService 显式接收 EventService；删除 SessionService 隐式获取网络的内部构造。
- 统一事件入口与整组注册的存储，调整网络释放后的入口检查；普通测试显式传入事件实例，不引入部分服务注册状态。用户已同意内部缓存构造入口，保持正式业务行为不变。
- 保留缓存与 Godot UI 的业务订阅边界，不新增 Dispose 前生命周期。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-services`: 构造注入、固定线性依赖与逆序清理，更新独立实例构造契约。
- `session-events`: 同一事件实例由组合入口创建并传给网络，统一全局注册入口。

## Impact

影响 NetService、SessionService、GameServices、缓存内部构造、服务组测试工厂及网络/缓存/生命周期测试。更新架构、路线图和规格；协议、玩法、UI 流程及消息分发规则不变。
