using Godot;

namespace Rpg.World.Components;

/// <summary>角色实体持有的移动目标组件。</summary>
internal sealed class DataMovementComponent
{
    /// <summary>当前目的地；无目标时为空。</summary>
    public Vector2? Target;

    /// <summary>以尚无移动目标的状态构造组件。</summary>
    public DataMovementComponent()
    {
        Target = null;
    }
}
