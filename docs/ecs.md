# ECS 核心

`scripts/core/ecs/` 提供与玩法无关的 C# 内存 ECS。调用方创建 `EcsWorld`，用单调递增的 `long` ID 表示实体；组件继承 `EcsComponent`，系统继承 `EcsSystem`。运行时不依赖场景节点，调用方负责在需要时调用 `Step(delta)`。

实体标识、有效实体集合和组件索引经核心 `IEcsWorldData` 接口读写。`scripts/data/EcsWorldData.cs` 提供本地实现，调用方创建后交给 `EcsWorld`；同一数据实例也可交给后续世界继续使用。系统注册与调度状态不属于该数据实例。

本地会话从 `EcsWorldDataRepo` 取得数据实例，再传给 `EcsWorld`。

```csharp
public partial class Position : EcsComponent
{
    public double X { get; set; }
}

public partial class MoveSystem : EcsSystem
{
    public override void Update(EcsWorld world, double delta)
    {
        foreach (var entityId in world.Query(typeof(Position)))
            world.GetComponent<Position>(entityId)!.X += delta;
    }
}

public static class Example
{
    public static void Run()
    {
        using var world = new EcsWorld(new EcsWorldData());
        var entityId = world.CreateEntity();
        world.AddComponent(entityId, new Position());
        world.AddSystem(new MoveSystem());
        world.Step(1.0 / 60.0);
        world.DestroyEntity(entityId);
    }
}
```

| 操作 | 行为 |
| --- | --- |
| `CreateEntity()`, `DestroyEntity(id)`, `HasEntity(id)` | 创建、销毁和检查实体。ID 不复用；无效 ID 的销毁返回 `false`。 |
| `AddComponent(id, component)` | 按组件实例的精确 C# 类型附加；同类型再次附加会替换旧实例。无效 ID 返回 `false`。 |
| `HasComponent<T>(id)`, `GetComponent<T>(id)`, `RemoveComponent<T>(id)` | 按静态类型操作；缺失时分别返回 `false`、`null`、`false`。也提供接受 `Type` 的重载。 |
| `Query(typeof(A), typeof(B))` | 返回同时具有指定类型的实体 ID 快照，按创建顺序排列。`Query()` 返回全部有效实体。 |
| `AddSystem(system)`, `RemoveSystem(system)`, `Step(delta)` | 系统按注册顺序每步执行一次。重复注册返回 `false`；更新中新增的系统下一步执行，已移除的系统立即跳过。 |

组件是可变引用；本版不跟踪字段变化。调用方负责管理创建的 Godot 对象，并避免让系统长期持有世界的强引用。`tools/check.ps1` 使用 Godot 4.7.2 .NET 版运行 `tests/core/TestEcs.cs` 的验收场景。

公开 API 的组件实例、组件类型和系统实例入参均要求非空。无效实体仍返回失败结果；缺失组件的 `GetComponent` 结果仍可为空，调用方在使用前应检查。
