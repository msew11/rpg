using System.Collections.Generic;

/// <summary>记录 ECS 调度顺序，并按测试场景修改注册集合。</summary>
public partial class TestRecordingSystem : EcsSystem
{
    /// <summary>记录本系统在调用日志中的名称。</summary>
    public string Tag { get; }

    /// <summary>共享的系统调用日志。</summary>
    public List<string> Calls { get; }

    /// <summary>验证更新时收到的世界实例。</summary>
    public EcsWorld ExpectedWorld { get; }

    /// <summary>验证更新时收到的时间增量。</summary>
    public double ExpectedDelta { get; }

    /// <summary>保存可选的系统注册变更场景。</summary>
    private (EcsWorld World, EcsSystem ToAdd, EcsSystem ToRemove)? _registrationChange;

    /// <summary>初始化系统名称、共享日志和预期调度参数。</summary>
    public TestRecordingSystem(string tag, List<string> calls, EcsWorld expectedWorld, double expectedDelta)
    {
        Tag = tag;
        Calls = calls;
        ExpectedWorld = expectedWorld;
        ExpectedDelta = expectedDelta;
    }

    /// <summary>指定更新期间需要执行的系统注册变更。</summary>
    public void SetRegistrationChange(EcsWorld world, EcsSystem toAdd, EcsSystem toRemove) =>
        _registrationChange = (world, toAdd, toRemove);

    /// <summary>验证调度参数并执行预设的注册变更。</summary>
    public override void Update(EcsWorld world, double delta)
    {
        Calls.Add(Tag);
        if (!ReferenceEquals(world, ExpectedWorld) || delta != ExpectedDelta)
            throw new System.InvalidOperationException("System received wrong world or delta.");
        if (_registrationChange is not { } change)
            return;
        change.World.RemoveSystem(change.ToRemove);
        change.World.AddSystem(change.ToAdd);
        _registrationChange = null;
    }
}
