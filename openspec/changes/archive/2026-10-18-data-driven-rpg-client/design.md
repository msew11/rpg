## Context

`develop` 当前只有空白 Godot 4.7.2 .NET 2D 主场景，无客户端运行时。目标是先建立可运行的数据驱动纵切流程，服务端和玩法规则仍未定义。

## Goals / Non-Goals

**Goals:** JSON 配置统一加载；选角后的数据会话；Action 唯一入口的 ECS 世界；独立 HUD；系统与角色设置持久化。

**Non-Goals:** 实际网络、安全认证、寻路、技能执行、背包操作、美术风格和远程配置更新。

## Decisions

- `RepoGameConfig` 递归枚举 `res://config` 下的 JSON（Schema 文件放在目录外），按 `kind` 选择已登记的类型和 Draft 2020-12 Schema，使用 JsonSchema.Net 校验、`System.Text.Json` 解码，并组合为 `DataGameConfig`。未知类型、重复类型和校验错误均阻止启动；避免静默遗漏文件。地图只存默认地块与稀疏覆盖，不展开百万格。
- 地图管理类统一负责坐标换算、边界和可见区域绘制。Godot 只为可见瓦片生成表现，地图尺寸固定示例为 1000×1000，瓦片为 32 像素；不为瓦片生成 ECS 实体。
- 模拟状态源负责 `demo/demo` 登录、角色列表、初始角色状态和延迟移动确认。角色列表存在登录流程，选角后 `GameSession` 创建该角色的 `DataCache`。缓存存权威最新状态并发出变更通知；世界协调器把变更转换为 Action，UI 可直接订阅缓存。
- `InputSystem` 是独立输入模块，负责把未由 UI 消费的点击转成移动 Action，未来也可承接 UI 输入路由。`WorldManager` 封装轻量 ECS（整数实体 ID、组件存储、按固定顺序运行的系统和 Action 队列）。`_PhysicsProcess` 驱动处理队列及系统更新。先执行世界初始化 Action，再执行角色初始化 Action。模拟修正也产生同步 Action。表现节点只读取世界快照。
- 移动系统保存目标和未确认意图序号；新点击替换目标，按直线固定速度前进。模拟源返回确认位置及意图序号；同步 Action 按序号忽略旧确认并应用权威位置，然后继续朝最新未确认目标移动。
- HUD 和面板由独立 `CanvasLayer` 下的 Godot `Control` 构成，读取配置和缓存，不受世界摄像机影响；世界对象显示由 `Node2D` 构成。系统设置与角色设置分别用 `user://settings/system.json` 和 `user://settings/roles/<role_id>.json`；设置保存采用临时文件替换。HUD 位置按相对锚点和偏移比例保存，拖动时限制在视口内。

## Risks / Trade-offs

- [单文件全地图配置体积过大] → 使用默认地块与稀疏覆盖，只绘制可见区域。
- [预测与确认竞争导致回跳] → Action 和确认都带序号；过期确认忽略，新目标持续生效。
- [配置资源或设置文件损坏] → 配置失败时显示启动错误；设置损坏时记录警告并使用默认值，不自动覆盖原文件。
- [Godot UI 自动化能力有限] → 核心逻辑采用 C# 测试，并在 Windows 桌面手动验收交互。

## Migration Plan

保留 `scenes/main.tscn` 为入口，以新启动流程替换占位文字。可通过单个本地提交回滚；不修改 `main` 的空白框架。

## Open Questions

实际服务端协议、角色数值规则、地图素材及技能数据留待后续目标。
