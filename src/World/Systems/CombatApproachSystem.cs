using System;
using Godot;
using Rpg.World.Components;

namespace Rpg.World.Systems;

/// <summary>将持续攻击意图转换为复用移动系统的走近目的地。</summary>
internal sealed class CombatApproachSystem : IWorldSystem
{
    /// <summary>目标失效时取消，目标位置变化时只提交新的走近意图。</summary>
    public void Update(WorldManager world, double delta)
    {
        var intent = world.Entities.Get<DataCombatIntentComponent>(world.RoleEntity);
        if (intent.Target is not { } id) return;
        if (!world.Time.CanAct || !world.TryGetMonster(id, out var target))
        {
            world.CancelCombat(stopMovement: true);
            return;
        }
        var position = world.RolePosition;
        var goal = new Vector2(target.X, target.Y);
        var range = world.Entities.Get<DataAttributesComponent>(world.RoleEntity).AttackRange;
        if (intent.ChaseDestination is not null && position.DistanceTo(goal) <= range + 0.01) return;
        var direction = position - goal;
        var destination = direction.Length() > range
            ? goal + direction.Normalized() * (float)Math.Max(0, range - 0.05) : position;
        if (intent.ChaseDestination is { } previous && previous.DistanceSquaredTo(destination) < 0.01f) return;
        intent.ChaseDestination = destination;
        world.RequestMovement(destination, force: true);
    }
}
