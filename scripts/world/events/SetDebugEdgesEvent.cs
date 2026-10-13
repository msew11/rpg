using Godot;

/// <summary>设置区块边缘线的显示状态。</summary>
[GlobalClass]
public partial class SetDebugEdgesEvent : WorldCommandEvent
{
    /// <summary>创建边缘线状态事件。</summary>
    public static SetDebugEdgesEvent Create(bool value) => new() { enabled = value };

    /// <summary>期望的边缘线状态。</summary>
    public bool enabled { get; set; }
}
