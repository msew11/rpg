using System.Collections.Immutable;
using System.Linq;
using Rpg.Protocol;
using Rpg.Server.Modules.World.Data;

namespace Rpg.Server.Modules.World.Utils;

/// <summary>在地图邮箱内共用观察帧构建与发布基线更新。</summary>
internal static class WorldFrames
{
    /// <summary>比较完整分组并将同角色的公开变化合并为一条。</summary>
    private static RoleWorldUpdatePb Update(DataWorldRole current, DataWorldRole? before, bool full)
    {
        full |= before is null;
        return new() { RoleId = current.RoleId,
            Profile = full || current.Name != before!.Name || current.Level != before.Level || current.Gender != before.Gender || current.HeadId != before.HeadId
                ? new() { Name = current.Name, Level = current.Level, Gender = (RoleGenderPb)current.Gender, HeadId = current.HeadId } : null,
            Status = full || current.Health != before!.Health || current.MaxHealth != before.MaxHealth || current.Mana != before.Mana || current.MaxMana != before.MaxMana
                ? new() { Health = current.Health, MaxHealth = current.MaxHealth, Mana = current.Mana, MaxMana = current.MaxMana } : null,
            Position = full || current.MapId != before!.MapId || current.X != before.X || current.Y != before.Y || current.TargetX != before.TargetX || current.TargetY != before.TargetY || current.Speed != before.Speed
                ? current.Position() : null,
            Equipment = full || !current.Equipment.SequenceEqual(before!.Equipment)
                ? new() { EquippedItems = { current.Equipment } } : null };
    }
    /// <summary>一次读取并更新观察基线，全量与增量在邮箱内保持顺序。</summary>
    internal static WorldFramePb Build(WorldContext context, long id, long now, bool full)
    {
        var cache = context.Cache.Roles; var (self, before, chunks, visible) = cache.Observe(id);
        var objects = context.Cache.Objects.Value.Where(item => chunks.Contains(cache.Chunk(item.X, item.Y))).ToImmutableDictionary(item => item.ObjectId);
        var center = cache.Chunk(self.X, self.Y); var sequence = (before?.Sequence ?? 0) + 1;
        var frame = new WorldFramePb { Sequence = sequence, ServerTimeMs = now, AcknowledgedAction = self.AcknowledgedAction,
            Self = Update(self, before?.Self, full || before is null) };
        if (full || before is null)
        {
            frame.Snapshot = new() { Map = WorldEntrySnapshot.Build(context, self.X, self.Y) };
            frame.Snapshot.Roles.AddRange(visible.Values.Select(role => role.ToPb()));
        }
        else
        {
            var delta = new WorldDeltaPb(); frame.Delta = delta;
            if (center != before.Center)
            {
                var map = WorldEntrySnapshot.Build(context, self.X, self.Y);
                delta.Map = new() { Center = map.Center };
                delta.Map.EnteredChunks.AddRange(map.Chunks.Where(chunk => !before.Chunks.Contains((chunk.Coordinate.X, chunk.Coordinate.Y))));
                delta.Map.LeftChunks.AddRange(before.Chunks.Except(chunks).Select(chunk => new MapChunkCoordPb { X = chunk.X, Y = chunk.Y }));
            }
            foreach (var item in objects.Values)
            {
                if (!before.Chunks.Contains(cache.Chunk(item.X, item.Y))) continue;
                if (!before.Objects.TryGetValue(item.ObjectId, out var old) || old != item)
                {
                    delta.Map ??= new() { Center = new() { X = center.X, Y = center.Y } };
                    delta.Map.UpsertedObjects.Add(GroundItems.ToPb(item));
                }
            }
            foreach (var item in before.Objects.Values)
                if (chunks.Contains(cache.Chunk(item.X, item.Y)) && !objects.ContainsKey(item.ObjectId))
                {
                    delta.Map ??= new() { Center = new() { X = center.X, Y = center.Y } };
                    delta.Map.RemovedObjectIds.Add(item.ObjectId.ToString("N"));
                }
            foreach (var role in visible.Values)
            {
                if (!before.Visible.TryGetValue(role.RoleId, out var revision)) delta.EnteredRoles.Add(role.ToPb());
                else if (role.Revision != revision.Revision)
                {
                    var update = Update(role, revision, false);
                    if (update.Profile is not null || update.Status is not null || update.Position is not null || update.Equipment is not null)
                        delta.UpdatedRoles.Add(update);
                }
            }
            delta.LeftRoleIds.AddRange(before.Visible.Keys.Except(visible.Keys));
        }
        frame.Self.Equipment = null; frame.Self.Position = self.Position();
        cache.Published(id, sequence, center, chunks, visible, objects); return frame;
    }
}
