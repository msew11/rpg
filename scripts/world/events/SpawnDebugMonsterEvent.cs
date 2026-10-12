using Godot;

/// <summary>在指定位置附近的活跃区块生成测试怪物。</summary>
[GlobalClass]
public partial class SpawnDebugMonsterEvent : WorldCommandEvent
{
    /// <summary>创建指定附近位置的怪物生成事件。</summary>
    public static SpawnDebugMonsterEvent Create(Vector2 position) => new() { near_position = position };

    /// <summary>用于选择区块与附近候选格的位置。</summary>
    public Vector2 near_position { get; set; }

    /// <summary>成功生成后的怪物实体标识，失败时为零。</summary>
    public long entity_id { get; set; }
}
