using Godot;

namespace Rpg.World;

/// <summary>世界实体持有的地图组件。</summary>
internal sealed record DataMapComponent(MapManager Map);

/// <summary>角色实体持有的身份组件。</summary>
internal sealed record DataRoleComponent(long RoleId);

/// <summary>角色实体持有的当前位置组件。</summary>
internal sealed class DataPositionComponent
{
    /// <summary>角色在世界中的像素坐标。</summary>
    public Vector2 Value;

    /// <summary>按初始位置构造组件。</summary>
    public DataPositionComponent(Vector2 value) => Value = value;
}

/// <summary>角色实体持有的移动目标组件。</summary>
internal sealed class DataMovementComponent
{
    /// <summary>当前目的地；无目标时为空。</summary>
    public Vector2? Target;
}
