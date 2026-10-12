using Godot;

/// <summary>向展示调用方提供当前调试开关的只读视图。</summary>
[GlobalClass]
public partial class WorldDebugView : RefCounted
{
    /// <summary>只供视图读取的世界调试组件。</summary>
    private readonly WorldDebugComponent _source;

    /// <summary>供 Godot 注册只读视图类型。</summary>
    public WorldDebugView() => _source = new WorldDebugComponent();

    /// <summary>绑定管理器持有的调试组件。</summary>
    internal WorldDebugView(WorldDebugComponent source) => _source = source;

    /// <summary>读取区块边缘线开关。</summary>
    public bool edges => _source.edges;

    /// <summary>读取区块雷达开关。</summary>
    public bool radar => _source.radar;
}
