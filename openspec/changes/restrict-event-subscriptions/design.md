# Design

## Context

见 proposal.md。SessionService 构造中订阅 LoginRt，在回调内创建 LoginCache 和 RoleCache；WorldService 构造中注册三个普通阶段监听。MainController 已订阅登录与选角，GameHud 已订阅移动与推送。CacheBase 已提供缓存阶段注册和幂等取消机制，不需要新增事件层。

## Goals / Non-Goals

目标：移除服务的全部协议订阅和句柄，将首次登录也交给缓存；世界仍由 WorldService 拥有和修改，UI 只协调操作，不直接操作 ECS。

不改变协议、缓存优先规则、移动预测与确认规则、网络代次隔离或账号与角色退出语义；不新增包装监听器，不决定玩法。

## Decisions

### 登录请求前准备缓存

SessionService 在登录尝试时创建空 LoginCache 和 RoleCache，并在重登或返回登录时释放旧缓存。LoginCache 提供无响应构造并订阅 LoginRt，首次响应直接由自身处理；保留已有响应构造供隔离验收使用。SessionService 的认证状态由缓存状态读取，不注册任何登录委托。RoleCache 只有选角请求设置 RoleId 后才能接纳角色响应。登录失败仅保留未认证空状态，下一次尝试回收旧缓存。

这替代当前“成功回调中创建缓存”的时机，解决服务不订阅后首次响应无人处理的问题；不选择由普通 UI 转换首次摘要，避免破坏缓存异常跳过普通阶段的规则。内部 LoginCache 与 RoleCache 访问仍拒绝未认证账号，不能因空实例提前存在而开放选角操作。已有响应构造失败必须释放两个缓存的订阅。

### UI 调用明确的世界操作

MainController 在成功选角的普通回调中先检查已认证状态、角色身份和缓存就绪，再调用 WorldService.InitializeRole()，完成原 OnSelectedRole 的地图、角色实体及设置初始化，随后创建表现。

MainController 增加 MoveRt 和 RoleStateNotice 的普通阶段监听，过滤当前角色并调用 WorldService.SyncRole()；该方法读取 Session 的权威快照并提交 SyncRoleAction。两个操作保留释放检查。GameHud 只刷新数据显示，不重复提交世界同步。监听归主控制器持有，不能依赖 HUD 是否已挂载。

不把世界同步塞进 RoleCache，避免缓存承担 ECS 生命周期和设置职责；不把 Action 构造下放到 UI，保持世界服务边界。

### 事件入口生命周期

保留唯一 GameServices.Events、CacheBase 的底层注册机制和 EventService 的分发实现。服务不得通过 Subscribe、事件 += 或辅助委托持有业务监听。网络发布与 ResetPending 仍允许。_events 仍用于独立事件域及业务入口撤销后的网络清理，不能仅因移除服务监听就直接改为 Current.Events。

## Risks / Trade-offs

- [空缓存与已认证账号混淆] → 以 IsLoggedIn 判断认证，保留未认证操作拒绝和失败重试验收。
- [世界同步依赖 UI 协调] → 由常驻 MainController 统一监听；服务测试显式调用世界操作，Godot 场景验收真实事件链路。
- [重复注册、非法响应或重试泄漏] → 缓存构造失败回收、重登回收和场景重建验收覆盖；保持缓存异常阻止普通阶段。
- [旧 revision 或其他角色进入世界] → 沿用缓存过滤与 Action 确认序号校验，并验证旧快照不会覆盖新世界状态。

## Migration Plan

先调整缓存和会话生命周期，再将世界监听改为操作接口并迁移至 MainController；同步服务、缓存、场景测试及架构、路线图。运行 tools/check.ps1 和 tools/test.ps1，验证规格，完成后同步并归档 change，创建中文约定式本地提交。回滚通过还原该提交完成。
