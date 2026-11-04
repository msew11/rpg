using System;
using System.Collections.Generic;
using Godot;
using Rpg.Session.Data;
using Rpg.World.Actions;
using Rpg.World.Components;

namespace Rpg.World;

/// <summary>接纳怪物权威生命及独立于实体生命周期的伤害表现。</summary>
internal sealed partial class WorldManager
{
    /// <summary>目标销毁后仍可消费的确认伤害。</summary>
    private readonly Queue<DataCombatHit> _damageNumbers = new();
    /// <summary>每名攻击者最后接纳的命中身份。</summary>
    private readonly Dictionary<long, (long Time, ulong Id)> _hitVersions = new();
    /// <summary>当前世界已经确认死亡的对象版本，阻止旧快照复活。</summary>
    private readonly Dictionary<Guid, long> _deadObjects = new();

    /// <summary>一次取走世界接纳的确认伤害，不引用怪物节点。</summary>
    internal DataCombatHit[] TakeDamageNumbers()
    {
        var result = _damageNumbers.ToArray();
        _damageNumbers.Clear();
        return result;
    }

    /// <summary>保持动态实体身份，只应用更新的对象版本。</summary>
    private void SyncMapValue(int entity, DataMapObjectPbData value)
    {
        var component = _entities.Get<DataMapObjectComponent>(entity);
        if (component.Revision >= value.Revision) return;
        _entities.Get<DataPositionComponent>(entity).Value = new Vector2(value.X, value.Y);
        component.Revision = value.Revision;
        component.MonsterTypeId = value.MonsterTypeId;
        component.ItemId = value.ItemId;
        component.Count = value.Count;
        if (value.MonsterTypeId is not null)
        {
            var health = _entities.Find<DataHealthComponent>(entity);
            if (health is null) _entities.Set(entity, new DataHealthComponent(value.Health, value.MaxHealth));
            else { health.Current = value.Health; health.Maximum = value.MaxHealth; }
        }
    }

    /// <summary>保留死亡墓碑并移除现有实体，不在客户端推算死亡。</summary>
    private void RemoveDeadMonster(Guid id, long revision)
    {
        if (_deadObjects.TryGetValue(id, out var old) && old >= revision) return;
        _deadObjects[id] = revision;
        if (_mapEntities.Remove(id, out var entity)) _entities.Remove(entity);
    }

    /// <summary>权威命中按身份去重，更新生命并保留致死伤害的位置。</summary>
    private void AcceptHit(SyncCombatHitAction action)
    {
        var hit = action.Hit;
        if (_hitVersions.TryGetValue(hit.RoleId, out var version) &&
            (version.Time > action.ServerTimeMs || version.Time == action.ServerTimeMs && version.Id >= hit.AttackId)) return;
        _hitVersions[hit.RoleId] = (action.ServerTimeMs, hit.AttackId);
        var target = action.Target;
        var currentRevision = _mapEntities.TryGetValue(target.ObjectId, out var entity)
            ? _entities.Get<DataMapObjectComponent>(entity).Revision : -1;
        if (!_deadObjects.ContainsKey(target.ObjectId) && target.Revision >= currentRevision)
        {
            if (hit.Dead) RemoveDeadMonster(target.ObjectId, target.Revision);
            else if (entity != 0) SyncMapValue(entity, target);
            if (MapObjects is { } objects) PublishMapObjects(objects.Sequence);
        }
        _damageNumbers.Enqueue(hit);
    }
}
