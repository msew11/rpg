using Godot;

/// <summary>切换世界位置所在的测试地块。</summary>
[GlobalClass]
public partial class ToggleTileEvent : WorldCommandEvent
{
    /// <summary>创建指定世界位置的地块事件。</summary>
    public static ToggleTileEvent Create(Vector2 position) => new() { world_position = position };

    /// <summary>待切换地块的世界位置。</summary>
    public Vector2 world_position { get; set; }
}
