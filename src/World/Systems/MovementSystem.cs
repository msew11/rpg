using Rpg.World.Components;

namespace Rpg.World.Systems;

/// <summary>按照角色目标进行直线移动的逻辑系统。</summary>
internal sealed class MovementSystem : IWorldSystem
{
    /// <summary>朝当前目的地推进并在到达时清除目标。</summary>
    public void Update(EntityStore entities, int roleEntity, double delta)
    {
        var position = entities.Get<DataPositionComponent>(roleEntity);
        var movement = entities.Get<DataMovementComponent>(roleEntity);
        if (movement.Target is not { } target) return;
        position.Value = position.Value.MoveToward(target, (float)(180 * delta));
        if (position.Value.DistanceTo(target) < 0.01f) movement.Target = null;
    }
}
