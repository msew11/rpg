using System;
using Rpg.Const;
using Rpg.World.Components;

namespace Rpg.World.Systems;

/// <summary>统一合成本人与远端角色的动作、朝向及攻击进度。</summary>
internal sealed class ActorMotionSystem : IWorldSystem
{
    /// <summary>先采用基础动作，再由有效攻击快照生成最终表现状态。</summary>
    public void Update(WorldManager world, double delta)
    {
        foreach (var entity in world.ActorEntities)
        {
            var actor = world.Entities.Get<DataActorComponent>(entity);
            var movement = world.Entities.Get<DataMovementComponent>(entity);
            actor.Motion = actor.RequestedMotion ?? (movement.Moving ? RoleMotion.Run : RoleMotion.Stand);
            actor.Facing = movement.Facing;
            actor.MotionVersion = actor.RequestedMotionVersion;
            actor.AttackDurationMs = 0;
            actor.AttackElapsedMs = 0;
            var attack = world.Entities.Get<DataAttackComponent>(entity).Current;
            if (attack is null || world.Time.Now >= attack.StartMs + attack.DurationMs) continue;
            var facing = attack.TargetPosition - world.Entities.Get<DataPositionComponent>(entity).Value;
            actor.Motion = attack.Motion;
            if (facing.LengthSquared() > 0) actor.Facing = facing.Normalized();
            actor.MotionVersion = checked((long)attack.AttackId);
            actor.AttackDurationMs = attack.DurationMs;
            actor.AttackElapsedMs = Math.Max(0, world.Time.Now - attack.StartMs);
        }
    }
}
