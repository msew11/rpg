using System;
using Rpg.World.Components;

namespace Rpg.World.Systems;

/// <summary>按照角色目标与属性推进位置，不写最终动作。</summary>
internal sealed class MovementSystem : IWorldSystem
{
    /// <summary>朝目的地推进并记录移动结果，到达后清除目标。</summary>
    public void Update(WorldManager world, double delta)
    {
        var entities = world.Entities;
        var roleEntity = world.RoleEntity;
        var position = entities.Get<DataPositionComponent>(roleEntity);
        var movement = entities.Get<DataMovementComponent>(roleEntity);
        var speed = entities.Get<DataAttributesComponent>(roleEntity).MovementSpeed;
        if (movement.Target is not { } target) { movement.Moving = false; return; }
        var previous = position.Value;
        position.Value = position.Value.MoveToward(target, (float)Math.Min(position.Value.DistanceTo(target), speed * delta));
        var displacement = position.Value - previous;
        if (displacement.LengthSquared() > 0) movement.Facing = displacement.Normalized();
        if (position.Value.DistanceTo(target) < 0.01f)
        {
            movement.Target = null;
            movement.Moving = false;
        }
        else if (delta > 0) movement.Moving = speed > 0;
    }
}
