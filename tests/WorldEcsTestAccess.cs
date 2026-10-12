using System;
using Godot;

/// <summary>为 openspec/specs/world-script-boundaries 的边界验收提供 ECS 观察与样本布置入口。</summary>
[GlobalClass]
public partial class WorldEcsTestAccess : RefCounted
{
    /// <summary>从会话缓存取得测试用 ECS 视图，不注册或调度世界 System。</summary>
    public static EcsWorld FromCache(LocalDataRepoCache cache)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var repo = cache.GetRepo(typeof(EcsWorldDataRepo)) as EcsWorldDataRepo;
        return new EcsWorld(repo?.Data ?? throw new InvalidOperationException("Missing ECS world repository."));
    }
}
