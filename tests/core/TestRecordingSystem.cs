using System.Collections.Generic;

/// <summary>记录 ECS 调度顺序，并按测试场景修改注册集合。</summary>
public partial class TestRecordingSystem : EcsSystem
{
    /// <summary>记录本系统在调用日志中的名称。</summary>
    public string Tag { get; }

    /// <summary>共享的系统调用日志。</summary>
    public List<string> Calls { get; }

    /// <summary>验证更新时收到的世界实例。</summary>
    public EcsWorld? ExpectedWorld { get; set; }

    /// <summary>验证更新时收到的时间增量。</summary>
    public double ExpectedDelta { get; set; }

    /// <summary>指定更新期间修改注册集合的世界。</summary>
    public EcsWorld? WorldToChange { get; set; }

    /// <summary>指定更新期间需要注册的系统。</summary>
    public EcsSystem? SystemToAdd { get; set; }

    /// <summary>指定更新期间需要移除的系统。</summary>
    public EcsSystem? SystemToRemove { get; set; }

    /// <summary>初始化系统名称与共享日志。</summary>
    public TestRecordingSystem(string tag, List<string> calls)
    {
        Tag = tag;
        Calls = calls;
    }

    /// <summary>验证调度参数并执行预设的注册变更。</summary>
    public override void Update(EcsWorld world, double delta)
    {
        Calls.Add(Tag);
        if (!ReferenceEquals(world, ExpectedWorld) || delta != ExpectedDelta)
            throw new System.InvalidOperationException("System received wrong world or delta.");
        if (WorldToChange is null)
            return;
        WorldToChange.RemoveSystem(SystemToRemove);
        WorldToChange.AddSystem(SystemToAdd);
        WorldToChange = null;
    }
}
