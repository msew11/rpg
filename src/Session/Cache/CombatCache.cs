using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Rpg.Const;
using Rpg.Protocol;
using Rpg.Session.Data;

namespace Rpg.Session.Cache;

/// <summary>会话内战斗通知的缓存接纳与幂等处理，不持有Godot节点。</summary>
internal sealed class CombatCache : CacheBase
{
    /// <summary>借用同一账号的角色缓存。</summary>
    private readonly RoleCache _role;
    /// <summary>借用同一账号的地图缓存。</summary>
    private readonly MapCache _map;
    /// <summary>按角色记录最近攻击。</summary>
    internal Dictionary<long, DataAttack> Attacks { get; } = new();
    /// <summary>取消后仍保留编号，防止迟到起手复活动作。</summary>
    private readonly Dictionary<long, (long Start, ulong Id)> _versions = new();
    /// <summary>已经接纳的命中身份，避免重复跳字。</summary>
    private readonly Dictionary<long, (long Time, ulong Id)> _hits = new();
    /// <summary>尚未被世界表现取走的伤害。</summary>
    private readonly Queue<DataCombatHit> _pending = new();
    /// <summary>本人最近起手响应。</summary>
    internal DataAttackReply? Reply { get; private set; }
    /// <summary>注册缓存阶段监听，节点只消费验证后的数据。</summary>
    internal CombatCache(RoleCache role, MapCache map)
    {
        _role = role; _map = map;
        Subscribe<AttackStartedNotice>(AcceptStart);
        Subscribe<AttackRt>(reply => {
            if (!_role.IsInitialized) return;
            var attack = reply.Accepted ? Parse(reply.Started) : null;
            Reply = new(reply.AttackId, reply.Accepted, reply.Reason, attack);
            if (reply.Accepted) AcceptStart(reply.Started);
        });
        Subscribe<AttackCancelledNotice>(notice => {
            if (!Current(notice.MapId) || notice.AttackId == 0 || notice.RoleId <= 0) return;
            if (_versions.TryGetValue(notice.RoleId, out var version) && (version.Start > notice.StartTimeMs || version.Start == notice.StartTimeMs && version.Id > notice.AttackId)) return;
            _versions[notice.RoleId] = (notice.StartTimeMs, notice.AttackId); Attacks.Remove(notice.RoleId);
        });
        Subscribe<AttackHitNotice>(AcceptHit);
        Subscribe<SessionKickedNotice>(_ => Reset());
    }
    /// <summary>只接受当前角色世界中的事件。</summary>
    private bool Current(int mapId) => _role.IsInitialized && _role.State.MapId == mapId;
    /// <summary>复制并验证动作，网络对象不进入只读快照。</summary>
    private static DataAttack Parse(AttackStartedNotice value)
    {
        if (value is null || value.RoleId <= 0 || value.AttackId == 0 || !Guid.TryParseExact(value.TargetId, "N", out var target) ||
            value.StartTimeMs <= 0 || !double.IsFinite(value.DurationMs) || value.DurationMs <= 0 ||
            !double.IsFinite(value.HitTimeMs) || value.HitTimeMs <= value.StartTimeMs || value.HitTimeMs >= value.StartTimeMs + value.DurationMs ||
            value.Motion is < 4 or > 6 || !float.IsFinite(value.TargetX) || !float.IsFinite(value.TargetY)) throw new InvalidDataException("攻击起手通知无效");
        return new(value.RoleId, value.AttackId, target, value.StartTimeMs, value.DurationMs, value.HitTimeMs,
            (RoleMotion)value.Motion, new(value.TargetX, value.TargetY));
    }
    /// <summary>按攻击编号接纳权威起手，旧或已取消的动作不复活。</summary>
    private void AcceptStart(AttackStartedNotice value)
    {
        if (!Current(value.MapId)) return;
        var attack = Parse(value);
        if (_versions.TryGetValue(attack.RoleId, out var version) && (version.Start > attack.StartMs || version.Start == attack.StartMs && version.Id >= attack.AttackId)) return;
        _versions[attack.RoleId] = (attack.StartMs, attack.AttackId); Attacks[attack.RoleId] = attack;
    }
    /// <summary>先验证完整目标再原子更新覆盖层和跳字队列。</summary>
    private void AcceptHit(AttackHitNotice value)
    {
        if (!Current(value.MapId)) return;
        var target = DataMapObjectPbData.FromPb(value.Target);
        if (value.AttackId == 0 || value.RoleId <= 0 || value.Damage < 0 || target.MonsterTypeId is null || target.MaxHealth <= 0 ||
            value.Dead != (target.Health == 0)) throw new InvalidDataException("攻击命中通知无效");
        if (_hits.TryGetValue(value.RoleId, out var last) && (last.Time > value.ServerTimeMs || last.Time == value.ServerTimeMs && last.Id >= value.AttackId)) return;
        _hits[value.RoleId] = (value.ServerTimeMs, value.AttackId);
        _map.AcceptHit(target);
        _pending.Enqueue(new(value.RoleId, value.AttackId, value.Damage, new(target.X, target.Y), value.Dead));
    }
    /// <summary>世界表现一次取走已确认的伤害。</summary>
    internal DataCombatHit[] TakeHits() { var result = _pending.ToArray(); _pending.Clear(); return result; }
    /// <summary>切角和离开时清除所有短期状态。</summary>
    internal void Reset() { Attacks.Clear(); _versions.Clear(); _hits.Clear(); _pending.Clear(); Reply = null; }
    /// <summary>释放不影响其他会话的缓存。</summary>
    protected override void OnDispose() => Reset();
}
