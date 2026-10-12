# Design

## Context

`LocalSession._start()` 分别创建玩家、恢复朝向并初始化区块。生产代码没有在这之后调用直接 setter；各项运行时交互已经发送事件。现有测试借直接 setter 布置跨区块、回环、通知和存档场景。

## Goals / Non-Goals

**Goals:** 给新游戏与有效存档恢复一个一次性初始化入口；隐藏零散写入和显式卸载；保留测试布置能力及既有区块跟随、通知和存档验收。

**Non-Goals:** 改变事件路由结果、通知归属或会话交互转发方式；这些按路线图后续 change 处理。

## Decisions

- `WorldManager.initialize_session(position, speed, facing, cfg)` 按原顺序创建玩家、设置恢复朝向并首次加载区块，返回玩家 ID；成功后拒绝再次调用。相比公开多步初始化，调用方无法在运行期单独重配某一步。
- 把 `create_player`、`initialize_test_chunks`、位置、朝向和地块直接写入方法改为 C# `internal`；显式卸载改为 `private`，只由 `step()` 的区块同步流程调用。原 setter 保留去重与通知逻辑，测试验收不用改写行为。
- `tests/WorldEcsTestAccess.cs` 增加公开的测试布置方法，调用这些内部方法。既有 GDScript 测试经该类设置场景，生产脚本只调用一次性初始化和事件入口。

## Risks / Trade-offs

- C# `internal` 在同一程序集内仍可被其他 C# 代码调用 → 目前生产调用面没有这些调用；公开给 GDScript 的世界 API 不再提供零散入口，后续如拆程序集可进一步限制。
- 测试辅助类仍编入项目程序集 → 它位于 `tests/` 且不被生产脚本引用，便于当前 Godot 无界面测试继续验证通知和持久化。
