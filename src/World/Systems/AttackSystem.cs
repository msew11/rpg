using Godot;
using Rpg.Session.Data;
using Rpg.World.Components;

namespace Rpg.World.Systems;

/// <summary>按范围、冷却与响应状态建立单次预测，只输出起手请求。</summary>
internal sealed class AttackSystem : IWorldSystem
{
    /// <summary>预测与权威位置均入范围才起手，不决定命中或伤害。</summary>
    public void Update(WorldManager world, double delta)
    {
        var entities = world.Entities;
        var entity = world.RoleEntity;
        var intent = entities.Get<DataCombatIntentComponent>(entity);
        var attack = entities.Get<DataAttackComponent>(entity);
        var cooldown = entities.Get<DataCombatCooldownComponent>(entity);
        var attributes = entities.Get<DataAttributesComponent>(entity);
        if (!world.Time.CanAct || intent.Target is not { } id || attack.Pending ||
            world.Time.Now < cooldown.NextAttackAt || attributes.AttacksPerSecond <= 0 || !world.TryGetMonster(id, out var target)) return;
        var position = entities.Get<DataPositionComponent>(entity);
        var goal = new Vector2(target.X, target.Y);
        if (position.Value.DistanceTo(goal) > attributes.AttackRange + 0.01 ||
            position.AuthorityValue.DistanceTo(goal) > attributes.AttackRange + 0.01) return;
        var duration = Rpg.Utils.CombatUtil.Duration(attributes.AttacksPerSecond);
        var idNumber = checked((ulong)world.NextActionId());
        var role = entities.Get<DataRoleComponent>(entity);
        attack.Current = new DataAttack(role.RoleId, idNumber, id, (long)world.Time.Now,
            duration, world.Time.Now + duration * attributes.HitRatio, attributes.AttackMotion, goal);
        attack.Pending = true;
        attack.Confirmed = false;
        cooldown.NextAttackAt = world.Time.Now + duration;
        world.Emit(new DataAttackRequested(idNumber, id));
    }
}
