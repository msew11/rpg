using System;
using Rpg.ServerV2.Modules.Common.Cache;
using Rpg.ServerV2.Modules.Role.Data;

namespace Rpg.ServerV2.Modules.Role.Cache;

/// <summary>邮箱独占的临时移动状态，坐标只保存在角色基础表。</summary>
public sealed class DataCacheMovement : DataCache<DataRoleMovement>
{
    /// <summary>建立静止状态，不从存档恢复目标。</summary>
    private readonly DataCacheProjection _projection;
    /// <summary>独立移动验收自持投影状态。</summary>
    internal DataCacheMovement() : this(new()) { }
    /// <summary>角色工作区共用唯一公开投影。</summary>
    internal DataCacheMovement(DataCacheProjection projection) : base(new()) => _projection = projection;
    /// <summary>先按原速度推进到服务器时间，再接纳速度变化。</summary>
    internal void Advance(DataCacheRole role, long now, int speed)
    {
        var before = Value; var data = role.Value;
        var elapsed = Math.Max(0, now - before.TimeMs) / 1000d;
        var next = before with { TimeMs = Math.Max(now, before.TimeMs) };
        if (before.TimeMs > 0 && before.TargetX is { } x && before.TargetY is { } y)
        {
            var dx = x - data.X; var dy = y - data.Y; var distance = Math.Sqrt(dx * dx + dy * dy);
            var amount = Math.Min(distance, elapsed * before.Speed);
            if (amount > 0)
            {
                role.Commit(data with { X = amount >= distance ? x : data.X + (float)(dx * amount / distance), Y = amount >= distance ? y : data.Y + (float)(dy * amount / distance) });
                _projection.Changed();
            }
            if (distance <= amount) { next = next with { TargetX = null, TargetY = null }; _projection.Changed(); }
        }
        if (next.Speed != speed) { next = next with { Speed = speed }; _projection.Changed(); }
        Commit(next);
    }
    /// <summary>仅较新的动作可以更改目标，零距离立即静止。</summary>
    internal void Move(DataCacheRole role, ulong action, float x, float y)
    {
        if (action <= Value.AcknowledgedAction) return;
        var stopped = role.Value.X == x && role.Value.Y == y;
        _projection.Changed();
        Commit(Value with { TargetX = stopped ? null : x, TargetY = stopped ? null : y, AcknowledgedAction = action });
    }
    /// <summary>控制屏障先推进旧目标，再停止并重置新控制者动作序号。</summary>
    internal void Stop(DataCacheRole role, long now, int speed)
    {
        Advance(role, now, speed);
        _projection.Changed();
        Commit(Value with { TargetX = null, TargetY = null, AcknowledgedAction = 0 });
    }
}
