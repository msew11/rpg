using System;
using System.Linq;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Rpg.ProtocolV2;
using Rpg.ServerV2.ActorProtocol;
using Rpg.ServerV2.Modules.World.Data;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.World.Handler;

/// <summary>地图本地处理器，构造有序观察帧，不反向查询角色。</summary>
internal sealed class WorldSyncHandler : IActorHandler<WorldContext, UpdateWorldRole, UpdateWorldRoleRt>,
    IActorHandler<WorldContext, BuildWorldUpdates, BuildWorldUpdatesRt>, IActorHandler<WorldContext, GetWorldSnapshot, GetWorldSnapshotRt>
{
    /// <summary>只接纳所属地图和较新版本的不可变角色公开数据。</summary>
    public ValueTask<UpdateWorldRoleRt> HandleAsync(WorldContext context, UpdateWorldRole request)
    {
        if (request.Role?.Position is null || request.Role.Position.MapId != context.Cache.MapId) throw new InvalidOperationException("公开投影地图不一致");
        context.Cache.Roles.Update(DataWorldRole.FromPb(request.Role, request.Revision, request.AcknowledgedAction));
        return ValueTask.FromResult(new UpdateWorldRoleRt());
    }
    /// <summary>只为脏观察者生成变化，无变化不产生空包。</summary>
    public ValueTask<BuildWorldUpdatesRt> HandleAsync(WorldContext context, BuildWorldUpdates request)
    {
        var reply = new BuildWorldUpdatesRt();
        foreach (var id in request.ObserverRoleIds)
            if (context.Cache.Roles.NeedsUpdate(id)) reply.Deliveries.Add(new WorldDeliveryPb { RoleId = id, Notice = new() { Frame = Build(context, id, request.ServerTimeMs, false) } });
        return ValueTask.FromResult(reply);
    }
    /// <summary>全量重同步与普通发布共用同一序号。</summary>
    public ValueTask<GetWorldSnapshotRt> HandleAsync(WorldContext context, GetWorldSnapshot request) =>
        ValueTask.FromResult(new GetWorldSnapshotRt { Frame = Build(context, request.RoleId, request.ServerTimeMs, true) });
    /// <summary>一次读取并更新观察基线，全量与增量在邮箱内保持顺序。</summary>
    private static WorldFramePb Build(WorldContext context, long id, long now, bool full)
    {
        var cache = context.Cache.Roles; var (self, before, chunks, visible) = cache.Observe(id);
        var objects = context.Cache.Objects.Value.Where(item => chunks.Contains(cache.Chunk(item.X, item.Y))).ToImmutableDictionary(item => item.ObjectId);
        var center = cache.Chunk(self.X, self.Y); var sequence = (before?.Sequence ?? 0) + 1;
        var frame = new WorldFramePb { Sequence = sequence, ServerTimeMs = now, AcknowledgedAction = self.AcknowledgedAction,
            Self = new() { RoleId = id, Position = self.Position() } };
        if (full || before is null)
        {
            frame.Snapshot = new() { Map = GetWorldEntrySnapshotHandler.Build(context, self.X, self.Y) };
            frame.Snapshot.Roles.AddRange(visible.Values.Select(role => role.ToPb()));
        }
        else
        {
            var delta = new WorldDeltaPb(); frame.Delta = delta;
            if (center != before.Center)
            {
                var map = GetWorldEntrySnapshotHandler.Build(context, self.X, self.Y);
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
                    delta.Map.UpsertedObjects.Add(GroundItemsHandler.ToPb(item));
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
                else
                {
                    if (!revision.Position().Equals(role.Position())) delta.MovedRoles.Add(new RolePositionUpdatePb { RoleId = role.RoleId, Position = role.Position() });
                    if (!revision.Equipment.SequenceEqual(role.Equipment)) delta.EquippedRoles.Add(new RoleEquipmentUpdatePb { RoleId = role.RoleId, Equipment = role.ToPb().Equipment });
                }
            }
            delta.LeftRoleIds.AddRange(before.Visible.Keys.Except(visible.Keys));
        }
        cache.Published(id, sequence, center, chunks, visible, objects); return frame;
    }
}
