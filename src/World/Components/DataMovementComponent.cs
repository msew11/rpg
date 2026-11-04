using Godot;

namespace Rpg.World.Components;

/// <summary>角色实体持有的移动目标组件。</summary>
internal sealed class DataMovementComponent
{
    /// <summary>当前目的地；无目标时为空。</summary>
    public Vector2? Target;
    /// <summary>最近有效移动朝向，停止时仍保留。</summary>
    public Vector2 Facing;
    /// <summary>最近一次系统推进后是否仍在移动。</summary>
    public bool Moving;

    /// <summary>以尚无移动目标的状态构造组件。</summary>
    public DataMovementComponent()
    {
        Target = null;
        Facing = new Vector2(1, 1);
        Moving = false;
    }
}
