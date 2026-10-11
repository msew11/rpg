using Godot;

/// <summary>保存世界调试视图的开关状态。</summary>
[GlobalClass]
public partial class WorldDebugComponent : EcsComponent
{
    /// <summary>是否显示加载区块的边缘线。</summary>
    public bool edges { get; set; }

    /// <summary>是否显示区块雷达。</summary>
    public bool radar { get; set; } = true;
}
