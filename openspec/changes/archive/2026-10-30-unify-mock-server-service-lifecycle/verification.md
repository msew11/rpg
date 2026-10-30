# 统一服务装配验证

日期：2026-10-30。

## 实现与验收

- GameServices 在 Setting 之后、Net 之前创建非空 MockServerService，公开 MockServer 入口；整体注册先撤销入口，再按逆序释放，包括 Net 之后的服务器服务。构造失败沿用逆序清理。
- Net 的正式构造接收现成 MockServerService、EventBusService、LogService。TCP 适配借用非空服务器服务；Net 提前释放只关闭玩家连接。旧协议和进程内回归仍显式组装。
- 全部11种 Service 的构造依赖参数与服务字段通过非空声明、无缺省值、只读字段和前置依赖检查；公开构造对每个服务参数逐一注入空值均立即拒绝。Config、Setting、Session、World 入口补齐空值或已释放检查。日志测试构造不再接受可空 EnvService。
- MockServerService 启动时存在，内部普通 MockServer 初始为空，仍在服务器模式登录时创建。纯客户端不创建服务器实例。

## 测试证据

- `EventSubscriptionBoundaryTests`：所有服务非空依赖声明、前置顺序、统一创建及严格逆序释放。
- `GameServicesTests`：正式启动服务器实例为空、Net 提前释放不连带释放服务器服务、整体释放和重启、所有公开服务构造拒绝空依赖。
- `ServerV2ServiceTests`：固定帧交付不变、Net 释放后服务器服务仍可建立新连接。
- `ServerV2TcpTests`：按需启动、复用、纯客户端隔离以及 Net 释放后真实 TCP 仍可连接原服务器。
- 双 Godot 进程烟测：admin 主机与 demo1 客户端互见并移动；原有启动失败清理和其他 UI 烟测保持通过。

执行 `powershell -ExecutionPolicy Bypass -File tools/check.ps1 -Tests` 全部通过：345 项 .NET 测试、323 项 Godot xUnit 测试及全部烟测，构建0警告0错误。

## 规格与文档

game-services、server-v2-account delta 已同步；本 change 严格校验通过，server-v2-account 主规格严格校验通过，game-services 普通校验通过，既有“环境参数统一管理”要求超过500字符的告警保留。未拆分该既有长要求。架构、服务器说明及路线图与实现一致；产品方向不变，无需更改愿景。`git diff --check` 通过。
