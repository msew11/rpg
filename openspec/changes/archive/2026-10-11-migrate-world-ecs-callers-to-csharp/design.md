## Context

本地 `main` 的 C# 核心已合入 `develop`。GDScript 组件和系统不能继承 C# 基类；`EcsWorld` 的泛型组件访问和 `System.Type` 参数也不适合由 GDScript 直接调用。现有视图大量读取 `WorldState` 的世界数据并订阅变化信号。

## Goals / Non-Goals

**Goals:** 将直接依赖 C# ECS 类型的世界状态、组件和系统迁移到 C#，使现有主场景和功能测试在 Godot .NET 版通过；保留视图和本地会话的 GDScript。

**Non-Goals:** 不迁移无 ECS 继承关系的 UI、输入、投影工具或手工区块配置；不改变玩法、美术与数据持久化行为。

## Decisions

- `WorldState` 与组件标记 `[GlobalClass]`，以 Godot `RefCounted` 类型提供可供 GDScript 访问的边界。世界方法与组件数据成员暂保留现有 snake_case 名称，减少视图修改；C# 内部调用 `EcsWorld` 的泛型 API。
- C# 系统只持有 ECS 更新逻辑，不持有世界引用或玩家 ID。世界坐标换算与移动方向通过一个小型 C# 静态工具实现，与现有 GDScript 工具保持同一公式，由现有移动和区块验收测试检验。
- `WorldState.initialize_test_chunks` 接收仍由 GDScript 实现的 `ManualChunkCfg` 对象，通过 Godot 对象边界只读取尺寸与 `tiles_for`。配置解析继续留在 GDScript。
- `WorldChunksComponent` 使用 Godot 集合保存展示用加载列表，地块数据仍按规范 ID 保存副本；变化信号由 C# 发出，GDScript 视图按实际 C# 信号名订阅。
- 功能测试保留现有 GDScript 行为场景；需要直接调用泛型 ECS API 的断言迁入 C# 测试，不向运行时加入仅供测试的接口。

## Risks / Trade-offs

- [C# 与 GDScript 边界成员不可见或类型推断失败] → 用 Godot .NET 编辑器导入和运行所有功能测试，针对边界进行局部适配。
- [C# `Vector2` 与 GDScript 浮点计算不同] → 保留相同公式并用现有八方向、速度和区块测试检验行为。
- [Godot 集合跨语言转换] → 保持 `Array[Vector2i]` 与 `PackedByteArray` 可访问形态，验证副本与加载范围场景。
