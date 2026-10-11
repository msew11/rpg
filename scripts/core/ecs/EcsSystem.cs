using Godot;

/// <summary>定义由 ECS 世界调度的系统基类。</summary>
[GlobalClass]
public abstract partial class EcsSystem : RefCounted
{
    /// <summary>处理一次世界更新，接收当前世界和时间增量。</summary>
    public abstract void Update(EcsWorld world, double delta);
}
