using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Rpg.Const;
using Rpg.Session.Data;

namespace Rpg.World;

/// <summary>按权威时间保存有限公开样本，延后100ms插值并限制外推。</summary>
internal sealed class RemoteRoleBuffer
{
    /// <summary>各可见角色最近样本。</summary>
    private readonly Dictionary<long, List<DataRemoteSample>> _samples = new();
    /// <summary>接纳一次完整可见集合，离开立即释放。</summary>
    internal void Accept(long time, IReadOnlyDictionary<long, DataPublicRole> roles)
    {
        foreach (var id in _samples.Keys.Except(roles.Keys).ToArray()) _samples.Remove(id);
        foreach (var role in roles.Values)
        {
            if (!_samples.TryGetValue(role.RoleId, out var samples)) _samples[role.RoleId] = samples = new();
            if (samples.Count > 0 && time <= samples[^1].Time) continue;
            samples.Add(new(time, role)); if (samples.Count > 5) samples.RemoveAt(0);
        }
    }
    /// <summary>所有坐标只向目标推进，不越过终点。</summary>
    internal IReadOnlyList<DataActorState> Render(double time)
    {
        var states = new List<DataActorState>(_samples.Count);
        foreach (var samples in _samples.Values)
        {
            var right = samples.FindIndex(sample => sample.Time >= time);
            var latest = right < 0 ? samples[^1] : samples[right]; var role = latest.Role;
            var position = role.Position.Position;
            if (right > 0)
            {
                var before = samples[right - 1]; var ratio = (float)Math.Clamp((time - before.Time) / (latest.Time - before.Time), 0, 1);
                position = before.Role.Position.Position.Lerp(position, ratio);
            }
            else if (right < 0 && role.Position.Target is { } target)
                position = position.MoveToward(target, role.Position.Speed * (float)Math.Clamp((time - latest.Time) / 1000, 0, .1));
            var direction = role.Position.Target is { } goal ? (goal - position).Normalized() : Vector2.Zero;
            states.Add(new(role.RoleId, role.Profile.Gender, position, direction == Vector2.Zero ? RoleMotion.Stand : RoleMotion.Run,
                direction == Vector2.Zero ? Vector2.Down : direction, 0, role.Profile.HeadId,
                role.Equipment.Items.FirstOrDefault(item => item.Slot == EquipmentSlot.MainHand)?.ItemId ?? 0,
                role.Equipment.Items.FirstOrDefault(item => item.Slot == EquipmentSlot.OffHand)?.ItemId ?? 0));
        }
        return states;
    }
    /// <summary>角色退出后清理样本。</summary>
    internal void Clear() => _samples.Clear();
    /// <summary>不可变权威样本。</summary>
    private sealed record DataRemoteSample(long Time, DataPublicRole Role);
}
