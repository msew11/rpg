using Godot;

/// <summary>报告一次世界事件的路由与命令状态变化。</summary>
[GlobalClass]
public partial class WorldEventResult : RefCounted
{
    /// <summary>本次事件是否有处理方法执行。</summary>
    public bool handled { get; }

    /// <summary>本次处理是否改变了世界命令状态。</summary>
    public bool changed { get; }

    /// <summary>供 Godot 注册结果类型；默认表示无处理者。</summary>
    public WorldEventResult() { }

    /// <summary>从路由与变化状态生成一次不可写的结果。</summary>
    internal WorldEventResult(bool wasHandled, bool didChange)
    {
        handled = wasHandled;
        changed = wasHandled && didChange;
    }
}
