# Design

## Context

NetService 通过 GameServices.Events 发布和重置，SessionService 的内部已有响应构造通过 GameServices.Net 获取网络。事件引用同时保存在 _events 和 Registration；普通 .NET 测试通过 RegisterEvents 单独注册事件域。

## Goals / Non-Goals

服务依赖必须全部由构造显式传入，顺序固定且无环；整体退出先撤销全局入口，消费者仅通过持有的实例清理。保持协议、缓存优先、业务订阅边界和游戏行为。不增加容器、自动排序、部分注册状态或释放前钩子。

## Decisions

- 构造顺序固定 Environment、Events、Config、Setting、Net、Session、World、Animation。Net 接收 Environment 与 Events；Session 接收 Net 与 Events，后者供其建立缓存。服务仅借用依赖，不负责释放被依赖服务。
- 缓存增加内部显式事件构造，CacheBase 保存该实例并托管订阅。公开缓存构造仍从 GameServices.Events 获取同一个事件域，Session 通过内部构造传入其绑定实例，保持创建时的事件域归属；不增加测试专用运行分支。普通测试自持事件实例并显式组装。
- 删除仅接收 LoginRt、隐式读取网络的 Session 构造。已有响应验收仍以显式 Net、Events、响应构造。
- 删除 _events、RegisterEvents 及提前发布，Events 与其余入口通过 Current 取得。Current 只验证整组注册存在，Net 属性自行检查网络状态，Events 属性自行检查事件释放状态；网络单独释放不阻断其他有效服务。
- 整体释放顺序严格与创建相反，先清空 _current，再逐项清理并收集异常。Session 清理调用所持 Net，Net 重置调用所持 Events，故不需要全局入口。
- 测试组装显式传入事件服务，不从网络暴露事件 getter 来倒推依赖。测试构造按同样顺序且失败回收自有实例。验证实际构造顺序、服务 IL 中无 GameServices 调用、共享唯一事件实例及逆序释放。

## Risks / Trade-offs

- [测试组装调用增多] → 更新现有工厂与普通测试事件作用域，不新增生产抽象层。
- [实例组装混用事件域] → 组合入口传递同一实例，并以共享消息及旧实例清理用例验收。
- [清理失败] → 保留逐项异常收集，验证网络或设置释放失败仍清理所有依赖。

## Migration Plan

完成构造注入与统一注册，迁移显式测试组装，补充架构约束回归并更新架构和路线图。运行 check.ps1、test.ps1 与客户端场景测试，验证并同步规格，归档 change，保留中文本地提交。
