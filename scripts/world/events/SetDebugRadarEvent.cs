using Godot;

/// <summary>设置雷达的显示状态。</summary>
[GlobalClass]
public partial class SetDebugRadarEvent : WorldCommandEvent
{
    /// <summary>创建雷达状态事件。</summary>
    public static SetDebugRadarEvent Create(bool value) => new() { enabled = value };

    /// <summary>期望的雷达状态。</summary>
    public bool enabled { get; set; }
}
