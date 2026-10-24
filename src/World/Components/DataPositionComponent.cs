using Godot;

namespace Rpg.World.Components;

/// <summary>角色实体持有的当前位置组件。</summary>
internal sealed class DataPositionComponent
{
    /// <summary>角色在世界中的像素坐标。</summary>
    public Vector2 Value;

    /// <summary>按初始位置构造组件。</summary>
    public DataPositionComponent(Vector2 value)
    {
        Value = value;
    }
}
