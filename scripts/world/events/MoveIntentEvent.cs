using Godot;

/// <summary>提交玩家在屏幕坐标中的移动方向。</summary>
[GlobalClass]
public partial class MoveIntentEvent : WorldCommandEvent
{
    /// <summary>创建指定实体的移动输入事件。</summary>
    public static MoveIntentEvent Create(long entityId, Vector2I direction) =>
        new() { entity_id = entityId, direction = direction };

    /// <summary>接收移动意图的实体。</summary>
    public long entity_id { get; set; }

    /// <summary>本次物理帧的屏幕方向。</summary>
    public Vector2I direction { get; set; }
}
