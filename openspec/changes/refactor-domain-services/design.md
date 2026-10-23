## Context

GameServices 已有显式注册和所有权句柄；保留其主线程约定，避免增加第二套隐式初始化。

## Goals / Non-Goals

业务服务拥有可变状态、业务方法和内部辅助对象。Godot 节点只保留节点引用、订阅句柄和显示所需状态。保持现有登录、选角、移动、设置及动画验收。不新增玩法和网络机制。

## Decisions

- IService 统一约定 Dispose；不引入泛型注册表或动态服务查找。GameServices 提供固定只读类型入口，初始化依赖明确可见。
- ConfigService 私有持有配置仓库，仅暴露已校验配置；SettingService 持有当前系统与角色设置，RepoSettings 仅做 JSON 读写。
- SessionService 私有持有账号 GameSession，处理登录结果并提供摘要、快照和选角方法。WorldService 私有持有 WorldManager、MapManager 和 InputSystem，处理选角初始化、移动和权威同步。
- AnimationService 私有持有动画仓库，ActorView 仅保留上次呈现的不可变快照及 Godot 动画节点。
- 服务间通过构造参数绑定本次启动实例，不在操作时重新查找全局服务。节点从 GameServices 访问；节点间表现引用、值类型和不可变呈现快照不受业务对象持有禁令限制。
- 注册前配置和设置加载成功；注册拒绝时调用方保留网络所有权。注册成功后统一以逆依赖顺序释放，即使某个释放失败也清理其他服务并撤销全局入口。
- SessionService 在缓存阶段建立账号，非法摘要阻止普通 UI 监听；普通阶段先运行 WorldService，再运行 UI 监听。HUD 退出先取消订阅并保存拖动，再清空角色设置。

## Reference

参考 Dalamud 的 `Dalamud/Service/Service{T}.cs`、`ServiceManager.cs` 和 `Plugin/Services/IDalamudService.cs`：统一服务契约、构造参数显式依赖、依赖先就绪及逆依赖卸载。当前仅六项主线程服务，以固定组合表达依赖顺序，保留用户要求的 GameServices 属性入口。

## Risks / Trade-offs

全局入口仅用于节点边界，隔离测试仍可独立构造网络和业务对象。旧节点必须在释放服务前完成订阅清理；全局服务释放后节点绘制应安全跳过。

## Validation

单元测试覆盖统一入口、重复注册、逆序清理及账号角色状态隔离；真实 Godot 场景覆盖启动失败、登录失败、选角失败恢复、移动校正、菜单切换、设置保存、动画及地图分块复用。执行 tools/check.ps1。
