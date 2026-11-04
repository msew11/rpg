using System;
using System.Linq;
using Rpg.Const;
using Rpg.Session.Data;
using Rpg.World.Actions;

namespace Rpg.World;

/// <summary>只在输入、网络通知与 ECS 出站事件之间转换，不保存攻击运行状态。</summary>
public sealed partial class WorldService
{
    /// <summary>从 ECS 读取持续攻击目标。</summary>
    public Guid? AttackTarget => _world?.AttackTarget;

    /// <summary>把左键怪物转换为持续攻击意图，即时发送世界生成的走近请求。</summary>
    public void AttackMonster(Guid id)
    {
        ThrowIfDisposed();
        if (!_session.CanMove || _world is not { } world) return;
        SyncCombat(world);
        world.Enqueue(new SetCombatIntentAction(id));
        world.FlushActions();
        DispatchWorldEvents(world);
    }

    /// <summary>把取消输入交给 ECS，同次输入取走取消和可选停止请求。</summary>
    public void CancelAttack(bool stopMovement = true)
    {
        if (_world is not { } world) return;
        SyncCombat(world);
        world.Enqueue(new CancelCombatAction(stopMovement));
        world.FlushActions();
        DispatchWorldEvents(world);
    }

    /// <summary>将配置、属性、时间及缓存已验证通知转换为纯数据 Action。</summary>
    private void SyncCombat(WorldManager world)
    {
        if (!_session.HasRole)
        {
            world.Enqueue(new SyncCombatContextAction(0, RolePosition, false, 0, 0, 0, RoleMotion.Attack1, 0));
            return;
        }
        var role = _session.Role;
        var weapon = _session.Equipment.Items.FirstOrDefault(item => item.Slot == EquipmentSlot.MainHand);
        var motion = weapon is null ? RoleMotion.Attack1 : _config.Equipment.Items[_config.Item.Items[weapon.ItemId].RelatedId].AttackMotion;
        var timing = _config.Basic.Value.AttackTiming(role.Gender, motion);
        world.Enqueue(new SyncCombatContextAction(_session.ServerTimeMs, role.Position, _session.CanMove,
            role.Speed, role.Attributes.Values[9], role.Attributes.Values[10], motion, timing.HitRatio));
        foreach (var update in _session.TakeCombatUpdates())
        {
            WorldAction action = update switch
            {
                DataAttackStartedUpdate started => new SyncAttackStartedAction(started.Attack),
                DataAttackCancelledUpdate cancelled => new SyncAttackCancelledAction(cancelled.RoleId, cancelled.AttackId, cancelled.StartMs),
                DataAttackReplyUpdate reply => new SyncAttackReplyAction(reply.Reply),
                DataCombatHitUpdate hit => new SyncCombatHitAction(hit.Hit, hit.Target, hit.ServerTimeMs),
                _ => throw new InvalidOperationException("未知战斗通知")
            };
            world.Enqueue(action);
        }
    }

    /// <summary>按世界产生顺序发送请求，移动沿用既有合并与确认机制。</summary>
    private void DispatchWorldEvents(WorldManager world)
    {
        foreach (var value in world.TakeEvents())
        {
            if (!_session.CanMove) continue;
            switch (value)
            {
                case DataMoveRequested move:
                    _pendingMove = move.Action;
                    _intentions.Clear();
                    _intentions[move.Action.ActionId] = move.Action;
                    SendPendingMove(move.Force);
                    break;
                case DataAttackRequested attack:
                    _session.Attack(attack.AttackId, attack.Target);
                    break;
                case DataAttackCancelRequested cancel:
                    _session.CancelAttack(cancel.ActionId);
                    break;
            }
        }
    }

    /// <summary>取走 ECS 接纳的权威伤害，怪物销毁后仍可显示。</summary>
    public DataCombatHit[] TakeDamageNumbers() => _world?.TakeDamageNumbers() ?? [];
}
