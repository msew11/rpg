using System;
using Rpg.Const;
using Rpg.World.Components;

namespace Rpg.World.Systems;

/// <summary>按照角色目标进行直线移动的逻辑系统。</summary>
internal sealed class MovementSystem : IWorldSystem
{
    /// <summary>从当前角色权威快照读取实际执行速度，保留小数。</summary>
    private readonly Func<double> _speed;

    /// <summary>绑定当前世界的移动速度来源。</summary>
    internal MovementSystem(Func<double> speed) => _speed = speed;

    /// <summary>朝当前目的地推进并在到达时清除目标。</summary>
    public void Update(EntityStore entities, int roleEntity, double delta)
    {
        var position = entities.Get<DataPositionComponent>(roleEntity);
        var movement = entities.Get<DataMovementComponent>(roleEntity);
        var actor = entities.Get<DataActorComponent>(roleEntity);
        if (movement.Target is not { } target) return;
        var previous = position.Value;
        position.Value = position.Value.MoveToward(target, (float)Math.Min(position.Value.DistanceTo(target), _speed() * delta));
        var displacement = position.Value - previous;
        if (displacement.LengthSquared() > 0)
        {
            actor.Facing = displacement.Normalized();
            actor.Motion = RoleMotion.Run;
        }
        if (position.Value.DistanceTo(target) < 0.01f)
        {
            movement.Target = null;
            actor.Motion = RoleMotion.Stand;
        }
    }
}
