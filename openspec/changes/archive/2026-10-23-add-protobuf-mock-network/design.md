# Design

## Context

现有同步状态源同时管理登录、会话和模拟移动；DataCache 单独保存角色快照并发 C# 事件。UI 固定帧仅在角色建立后推进。迁移需兼顾登录阶段通信、现有延迟校正、动作确认过滤和角色设置隔离。

## Goals / Non-Goals

Goals：提供可替换字节传输、缓存优先的消息分发及持久化服务端边界。Non-Goals：真实网络、RPC 等待、认证安全性、背包与玩法扩展。

## Decisions

- proto3 使用 Google.Protobuf 3.36.2；Grpc.Tools 2.84.0 仅参与构建，输出到 src/Net/Message 并提交。信封包含协议完整名称、会话代次和 payload；显式协议表校验发送方向，避免通过反射任意实例化类型。
- NetManager 持有 EventSystem、出入队列和 INetTransport。进程内 MockTransport 仅交换 byte[]；Tick 先发送当前队列，再推进服务端并处理接收队列。每次重置推进代次并清理旧队列；旧信封直接丢弃。监听中发送的新请求下一帧发送。
- EventSystem 对具体类型分缓存/普通两阶段，订阅句柄释放即移除；每条消息快照、嵌套发布排队，Reset 使当前分发快照失效。缓存异常跳过普通阶段，普通异常不阻断其他监听，错误通过独立事件报告。
- GameSession 构造时建立空 RoleCache，选角成功响应初始化；MainController 常驻监听登录/选角，成功后创建世界和 HUD。HUD 监听 MoveRt/RoleStateNotice，读取 RoleCache.State。角色快照继续使用现有客户端 DataRole/DataRoleState，不引用可变协议对象。
- MockServer 使用 Server/Data 下独立 DataServerRole，以数值坐标和独立性别枚举存储，不引用 Godot。RepoServerRole 负责 JSON 校验、初始数据和临时文件替换。每次权威提交先写文件再替换内存状态，移动失败回滚模拟位置及待处理移动。
- 保留 0.45 秒确认、每秒 180 逻辑单位和演示的横向小偏差。只持久化已经确认或主动推送的状态；会话结束放弃尚未确认的移动。重建会话重置动作确认序号但保留持久化 revision，避免新世界动作序号被旧确认阻塞。
- 单个活动角色；ResetSession 清理服务端活动角色和 pending，但保留演示登录结果供返回选角使用。主动更新入口接受独立服务端模型，强制增加 revision，成功落盘后发送 Notice。

## Risks / Trade-offs

- 进程内传输不覆盖真实连接问题 → 保留传输接口，真实网络另行设计。
- JSON 存储不提供多进程并发事务 → 本期只支持单实例 MockServer；同目录临时文件替换避免半写文件。
- 协议对象可变 → 客户端与服务端各自转换为独立不可变快照。
- 存档损坏 → 明确报错并保留文件，不以演示默认值静默覆盖。

## Migration Plan

一次迁移现有运行入口与相关测试，删除旧源，更新长期文档和规格。通过 xUnit、Godot 冒烟及 check.ps1 后归档并在 develop 本地提交；回滚代码可回退该提交，服务端存档不写入仓库。

## Verification

- `dotnet test tests/ClientTests/ClientTests.csproj --nologo`：36 项通过，包含 23 项新增协议、事件和服务端验收用例；覆盖保存失败、损坏存档、重启恢复和旧会话隔离。
- `dotnet build rpg.csproj --no-restore -t:Rebuild --nologo`：完整生成通过，0 警告、0 错误。
- `powershell -ExecutionPolicy Bypass -File tools/check.ps1`：C# 构建、Godot 导入和主场景启动通过。
- Godot headless 的 client_smoke、actor_animation_smoke、map_chunk_smoke 均通过；客户端冒烟额外验证损坏存档选角失败后可恢复重试。
- 当前 change 及三份新增主规格严格校验通过；全库普通规格校验 11/11 通过。全库严格校验仍有 7 份既有规格的 Purpose 占位或过短警告，按同步契约保留既有 Purpose，不混入无关修订。
