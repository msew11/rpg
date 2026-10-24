# Proposal

## Why

Net 与 Events 访问器单独检查释放状态，和其他服务入口不一致。缓存保存事件服务并使用内部构造传参，超出了缓存可通过全局入口订阅的既定边界。

## What Changes

- 全部访问器仅检查完整注册，IsInitialized 仅表示注册是否存在；服务业务方法自行检查释放状态。
- CacheBase 每次订阅通过 GameServices.Events 获取事件域，不持有服务引用；取消订阅继续使用已有句柄。
- SessionService 只通过构造绑定 Net，不再保存和传递缓存使用的 Events。
- 缓存相关测试先注册完整服务组，迁入既有 Godot 测试执行环境，不添加正式代码测试分支。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `game-services`: 统一访问与注册状态语义，账号构造只绑定 Net。
- `session-events`: 缓存仅通过全局入口订阅，移除显式事件构造。

## Impact

GameServices、SessionService、CacheBase 及其实现类、测试准备流程和架构文档。保持线性创建与逆序释放、服务禁止全局访问和订阅的约束。
