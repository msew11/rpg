## Context

GameServices 当前在 Initialize、两个 Register 和 Registration 构造之间分摊组装责任，测试还依赖内部环境注册与独立事件初始化。

## Goals / Non-Goals

**Goals:** 正式服务的 new 全部在无参 Initialize 内；Register 不创建服务；保留失败回收和逆序释放。

**Non-Goals:** 容器、配置化工厂、自动排序及新的业务行为。

## Decisions

Initialize 按 Environment、Events、Config、Setting、Net、Session、World、Animation 直接构造，用已有服务列表记录部分初始化结果以回收失败。Register 接收完整实例组，仅验证并创建所有权句柄；句柄构造仅赋值。内部 RegisterEvents 只接管测试创建的事件实例，不创建服务。隔离环境及传输的组装迁移到 tests 内的 GameServicesTestFactory，不为测试在正式入口增加参数或覆盖状态。

## Risks / Trade-offs

测试自行构造服务 → 复用现有行为验收，并在 Godot 场景中直接验证正式无参 Initialize 的默认环境、重复初始化和退出清理。事件提前建立 → 所有构造失败路径回收事件并撤销入口。
