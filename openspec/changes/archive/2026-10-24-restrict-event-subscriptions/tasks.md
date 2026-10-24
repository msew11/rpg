# Tasks

## 1. 缓存与会话

- [x] 1.1 为 LoginCache 增加空缓存构造并处理首次登录响应，移除 SessionService 的登录监听及句柄；验证 UI 提前订阅仍读取完整摘要，非法响应阻止普通阶段。
- [x] 1.2 调整登录尝试、重试、已有响应构造及退出的缓存创建与释放，保留未认证操作拒绝和网络代次隔离；更新会话与缓存测试，验证失败重试、切角保留摘要、构造失败回收和旧实例隔离。
- [x] 1.3 更新 docs/architecture.md 的账号缓存创建时机与订阅边界，核对与代码及 client-session delta 一致。

## 2. 世界与 Godot UI

- [x] 2.1 将 WorldService 三个协议监听改为显式初始化与同步操作，删除监听句柄；服务测试显式调用操作，验证地图先于角色、角色设置隔离和权威修正。
- [x] 2.2 在 MainController 选角监听中调用初始化，在新增移动与推送监听中调用同步；更新 Godot 场景验收，验证首帧表现、移动确认、主动推送和退出重建，监听不依赖 HUD 存在。
- [x] 2.3 增加订阅位置的架构回归检查，允许 CacheBase 的公共机制及其实现类、Godot UI，验证服务及其他正式业务类没有直接或包装订阅。
- [x] 2.4 更新 docs/architecture.md 的世界协调与事件流程，更新 docs/roadmap.md 的本次状态，核对服务仍拥有 ECS 且 UI 仅协调操作。

## 3. 整体验收

- [x] 3.1 运行 powershell -ExecutionPolicy Bypass -File tools/check.ps1 和 tools/test.ps1，记录完整检查及 Godot headless 结果；确认非法缓存消息、旧角色消息及重复释放不产生回归。
- [x] 3.2 运行 OpenSpec change 验证，复核目标、代码、规格及长期文档一致，无未完成任务后准备交付。

## Workflow follow-up

- 完成验证后同步规格并归档 change。
- 创建中文 Conventional Commits 本地提交，交付提交号、验证结果与限制；不推送。

## 验证记录

- tools/test.ps1（包含 tools/check.ps1）：构建零警告、零错误，62 个普通 .NET 用例与 20 个 Godot 用例全部通过。
- tests/client_smoke.tscn：登录失败重试、选角、角色表现、HUD 离树后的推送和移动同步、场景重建与菜单退出通过；启动失败场景产生一次预期错误日志。
- OpenSpec change 严格验证通过，全部 12 项主规格验证通过；原有 Purpose 占位和长需求提示保留。
