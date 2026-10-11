using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>验证 openspec/specs/ecs-runtime 的实体、组件、查询和调度场景。</summary>
public partial class TestEcs : SceneTree
{
    /// <summary>收集未满足的验收断言。</summary>
    private readonly List<string> _failures = [];

    /// <summary>运行核心 ECS 和数据仓库契约场景，并以退出码报告结果。</summary>
    public override void _Initialize()
    {
        try
        {
            RunEcsScenarios();
            RunDataRepoScenarios();
        }
        catch (Exception exception)
        {
            _failures.Add(exception.ToString());
        }

        foreach (var failure in _failures)
            GD.PrintErr(failure);
        if (_failures.Count == 0)
            GD.Print("Core C# tests passed.");
        Quit(_failures.Count == 0 ? 0 : 1);
    }

    /// <summary>覆盖 ecs-runtime 规定的实体、组件、查询及系统调度行为。</summary>
    private void RunEcsScenarios()
    {
        using var world = new EcsWorld();
        var first = world.CreateEntity();
        var second = world.CreateEntity();
        var third = world.CreateEntity();
        Check(first != second && second != third, "entity IDs are unique");
        Check(world.HasEntity(first), "created entity exists");
        Check(!world.HasEntity(999), "unknown entity is absent");
        using var rejected = new TestPositionComponent();
        Check(!world.AddComponent(999, rejected), "unknown entity rejects component");
        Check(!world.HasEntity(999), "component operation does not create entity");
        Check(world.GetComponent<TestPositionComponent>(first) is null, "missing component reads null");

        using var oldPosition = new TestPositionComponent(1);
        using var newPosition = new TestPositionComponent(2);
        using var velocity = new TestVelocityComponent();
        using var secondPosition = new TestPositionComponent();
        Check(world.AddComponent(first, oldPosition), "add position");
        Check(world.AddComponent(first, newPosition), "replace position");
        Check(ReferenceEquals(world.GetComponent<TestPositionComponent>(first), newPosition), "replacement is current component");
        Check(world.HasComponent<TestPositionComponent>(first), "has component");
        Check(world.AddComponent(first, velocity), "add velocity");
        Check(world.AddComponent(second, secondPosition), "add second position");
        Check(world.Query(typeof(TestPositionComponent), typeof(TestVelocityComponent)).SequenceEqual([first]), "multi-type query matches all");
        Check(world.Query(typeof(TestPositionComponent)).SequenceEqual([first, second]), "query preserves creation order");
        var snapshot = world.Query();
        Check(snapshot.SequenceEqual([first, second, third]), "empty query lists all entities");
        Check(world.RemoveComponent<TestVelocityComponent>(first), "remove existing component");
        Check(!world.RemoveComponent<TestVelocityComponent>(first), "removing absent component reports no change");
        Check(!world.HasComponent<TestVelocityComponent>(first), "removed component is absent");
        Check(world.Query(typeof(TestPositionComponent), typeof(TestVelocityComponent)).Length == 0, "query reflects component removal");
        Check(world.DestroyEntity(first), "destroy existing entity");
        Check(!world.DestroyEntity(first), "destroying twice reports no change");
        Check(!world.HasEntity(first), "destroyed entity is absent");
        Check(world.GetComponent<TestPositionComponent>(first) is null, "destroy discards components");
        Check(!world.AddComponent(first, rejected), "destroyed entity rejects components");
        Check(snapshot.SequenceEqual([first, second, third]), "query result is a snapshot");
        Check(world.Query().SequenceEqual([second, third]), "new query excludes destroyed entity");
        Check(world.CreateEntity() > third, "destroyed IDs are not reused");

        // 对应 ecs-runtime 的共享数据场景：重建操作入口后，实体和组件仍由同一数据实例承载。
        using (var sharedWorld = new EcsWorld(world.Data))
        {
            Check(sharedWorld.Query().SequenceEqual(world.Query()), "shared data preserves entity membership");
            Check(ReferenceEquals(sharedWorld.GetComponent<TestPositionComponent>(second), secondPosition),
                "shared data preserves component identity");
            Check(sharedWorld.CreateEntity() > third, "shared data preserves next entity ID");
        }

        var calls = new List<string>();
        using var alpha = new TestRecordingSystem("alpha", calls, world, 0.25);
        using var beta = new TestRecordingSystem("beta", calls, world, 0.25);
        using var gamma = new TestRecordingSystem("gamma", calls, world, 0.25);
        alpha.SetRegistrationChange(world, gamma, beta);
        Check(world.AddSystem(alpha), "register first system");
        Check(world.AddSystem(beta), "register second system");
        Check(!world.AddSystem(alpha), "duplicate system rejected");
        world.Step(0.25);
        Check(calls.SequenceEqual(["alpha"]), "removed system skipped and new system deferred");
        world.Step(0.25);
        Check(calls.SequenceEqual(["alpha", "alpha", "gamma"]), "systems follow registration order");
        Check(!world.RemoveSystem(beta), "removed system is no longer registered");
        Check(world.RemoveSystem(alpha), "remove registered system");
        world.Step(0.25);
        Check(calls.SequenceEqual(["alpha", "alpha", "gamma", "gamma"]), "removed system stays inactive");

        using var replacementWorld = new EcsWorld();
        var replacementCalls = new List<string>();
        using var changer = new TestRecordingSystem("changer", replacementCalls, replacementWorld, 0.25);
        using var readded = new TestRecordingSystem("readded", replacementCalls, replacementWorld, 0.25);
        changer.SetRegistrationChange(replacementWorld, readded, readded);
        replacementWorld.AddSystem(changer);
        replacementWorld.AddSystem(readded);
        replacementWorld.Step(0.25);
        Check(replacementCalls.SequenceEqual(["changer"]), "remove and re-add waits until next step");
        replacementWorld.Step(0.25);
        Check(replacementCalls.SequenceEqual(["changer", "changer", "readded"]), "re-added system follows new registration order");
    }

    /// <summary>覆盖 data-repository 的两种形态基类经统一接口返回类型与形态的场景。</summary>
    private void RunDataRepoScenarios()
    {
        IDataRepo single = new TestSingleRepo();
        IDataRepo multiple = new TestMultipleRepo();
        Check(single.GetDataType() == typeof(TestRepoData), "single repo returns generic data type");
        Check(single.GetStorageKind() == DataRepoStorageKind.Single, "single repo declares shape");
        Check(multiple.GetDataType() == typeof(TestRepoData), "multiple repo returns generic data type");
        Check(multiple.GetStorageKind() == DataRepoStorageKind.Multiple, "multiple repo declares shape");
        var worldRepo = new TestEcsWorldRepo();
        using var repoWorld = new EcsWorld(worldRepo);
        Check(ReferenceEquals(repoWorld.Data, worldRepo.Data), "ECS world uses repo-owned data");
        Check(repoWorld.CreateEntity() == 1, "ECS world accepts a single data repo");
    }

    /// <summary>记录未满足的验收断言。</summary>
    private void Check(bool condition, string description)
    {
        if (!condition)
            _failures.Add(description);
    }

    /// <summary>验证仓库泛型参数可承载的测试数据。</summary>
    private sealed class TestRepoData : IDataRepoData;

    /// <summary>验证单例形态声明的测试仓库。</summary>
    private sealed class TestSingleRepo : DataRepoSingle<TestRepoData>
    {
        /// <summary>测试单例仓库承载的数据。</summary>
        public override TestRepoData Data { get; } = new();
    }

    /// <summary>验证 ECS 世界能直接接收单例仓库。</summary>
    private sealed class TestEcsWorldRepo : DataRepoSingle<DataEcsWorld>
    {
        /// <summary>测试世界仓库承载的数据。</summary>
        public override DataEcsWorld Data { get; } = new();
    }

    /// <summary>验证多例形态声明的测试仓库。</summary>
    private sealed class TestMultipleRepo : DataRepoMultiple<TestRepoData>;
}
