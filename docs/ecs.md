# ECS 核心

`scripts/core/ecs/` 提供与玩法无关的 C# 内存 ECS。调用方创建 `EcsWorld`，用单调递增的 `long` ID 表示实体；组件继承 `EcsComponent`，系统继承 `EcsSystem`。运行时不依赖场景节点，调用方负责在需要时调用 `Step(delta)`。

交互事件继承 `EcsEvent`。系统用 `[EcsEventHandler(typeof(MyEvent))]` 标记 `void Handler(EcsWorld world, MyEvent value)` 方法；`AddSystem` 收集和校验处理方法，`SendEvent(event)` 按注册顺序同步分发给精确类型匹配的处理者，返回是否被处理。无效声明在注册时抛出参数异常；移除系统会注销其路由。事件与路由不属于持久化数据。

实体标识、有效实体集合、组件索引和父子关系经核心 `IEcsWorldData` 接口读写。调用方提供数据实例，也可把同一实例交给新的 `EcsWorld` 使用。系统注册与调度状态不属于该数据实例。

实体可用 `CreateEntity(parentId)` 指定父实体。`GetParent(id)` 和 `GetChildren(id)` 查询关系；销毁父实体会级联销毁所有后代及其组件。无效父标识无法创建子实体。

组件可覆写 `ToPersistedString()` 与 `FromPersistedString(string)` 提供双向落地；前者默认返回 `null`，表示无需持久化。核心不负责文件写入、实体记录格式或卸载策略。

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
    public static void Run(IEcsWorldData data)
    {
        using var world = new EcsWorld(data);
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
| `CreateEntity()`, `CreateEntity(parentId)`, `DestroyEntity(id)`, `HasEntity(id)` | 创建、级联销毁和检查实体。ID 不复用；无效 ID 的销毁返回 `false`。 |
| `AddComponent(id, component)` | 按组件实例的精确 C# 类型附加；同类型再次附加会替换旧实例。无效 ID 返回 `false`。 |
| `HasComponent<T>(id)`, `GetComponent<T>(id)`, `RemoveComponent<T>(id)` | 按静态类型操作；缺失时分别返回 `false`、`null`、`false`。也提供接受 `Type` 的重载。 |
| `Query(typeof(A), typeof(B))` | 返回同时具有指定类型的实体 ID 快照，按创建顺序排列。`Query()` 返回全部有效实体。 |
| `AddSystem(system)`, `RemoveSystem(system)`, `Step(delta)` | 系统按注册顺序每步执行一次。重复注册返回 `false`；更新中新增的系统下一步执行，已移除的系统立即跳过。 |

组件是可变引用；本版不跟踪字段变化。调用方负责管理创建的 Godot 对象，并避免让系统长期持有世界的强引用。`tools/check.ps1` 使用 Godot 4.7.2 .NET 版运行 `tests/core/TestEcs.cs` 的验收场景。

公开 API 的组件实例、组件类型和系统实例入参均要求非空。无效实体仍返回失败结果；缺失组件的 `GetComponent` 结果仍可为空，调用方在使用前应检查。
