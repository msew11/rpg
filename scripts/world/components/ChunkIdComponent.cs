using Godot;

/// <summary>标识活跃区块实体对应的规范区块坐标。</summary>
public partial class ChunkIdComponent : EcsComponent
{
    /// <summary>有限世界中的规范区块坐标。</summary>
    public Vector2I value { get; set; }

    /// <summary>创建默认坐标的区块标识组件。</summary>
    public ChunkIdComponent() { }

    /// <summary>创建指定规范坐标的区块标识组件。</summary>
    public ChunkIdComponent(Vector2I value) => this.value = value;
}
