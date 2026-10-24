# Proposal

## Why

SessionService 和 WorldService 当前直接订阅协议，与用户确定的边界不符：业务订阅只能位于 CacheBase 实现类和 Godot UI 类。需要明确缓存更新、界面通知和服务操作的分工，同时保持登录、选角和权威同步行为。

## What Changes

- 移除 SessionService 和 WorldService 的协议订阅及订阅句柄。
- LoginCache 负责首份登录响应，允许在请求前创建未认证的空缓存；RoleCache 继续在缓存阶段维护角色状态。
- Godot MainController 订阅选角、移动和推送，读取缓存后显式调用世界初始化与同步操作，再更新表现。
- 服务只提供操作和状态，不通过委托、辅助监听器或其他事件绕过订阅边界。
- 保留唯一事件域、缓存优先、非法缓存响应跳过普通阶段和重启隔离；同步架构文档、路线图及验收测试。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `session-events`: 明确业务订阅只允许缓存实现类和 Godot UI 类。
- `client-session`: 将首次登录响应处理归入 LoginCache，区分空缓存与已认证账号。

## Impact

涉及 SessionService、LoginCache、RoleCache 的构造时机，WorldService 的显式操作接口，MainController 的协议协调，以及服务、缓存和 Godot 场景测试。更新 docs/architecture.md 与 docs/roadmap.md；不改变协议、玩法、ECS 写入边界或网络传输。不因本次迁移直接删除 _events，其生命周期用途需独立验证。
