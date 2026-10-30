using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>从完整地图缓存投影初始九块，空块也返回且不改变保存基线。</summary>
public sealed class GetWorldEntrySnapshotHandler : IActorHandler<WorldContext, GetWorldEntrySnapshot, GetWorldEntrySnapshotRt>
{
    /// <summary>只按权威位置和配置裁剪范围，不注册成员或订阅。</summary>
    public ValueTask<GetWorldEntrySnapshotRt> HandleAsync(WorldContext context, GetWorldEntrySnapshot request)
        => ValueTask.FromResult(new GetWorldEntrySnapshotRt { Map = Build(context, request.X, request.Y) });
    /// <summary>同一地图回合内复用初始范围投影，不进行 IO。</summary>
    internal static MapAoiSnapshotPb Build(WorldContext context, float xPosition, float yPosition)
    {
        var layout = context.Cache.Layout;
        if (!float.IsFinite(xPosition) || !float.IsFinite(yPosition) || xPosition < 0 || yPosition < 0 ||
            xPosition >= (long)layout.Width * layout.TileSize || yPosition >= (long)layout.Height * layout.TileSize)
            throw new InvalidDataException("进入地图的权威位置无效");
        var size = (double)layout.TileSize * layout.ChunkSizeTiles;
        var cx = (int)(xPosition / size); var cy = (int)(yPosition / size);
        var columns = (layout.Width - 1) / layout.ChunkSizeTiles + 1;
        var rows = (layout.Height - 1) / layout.ChunkSizeTiles + 1;
        var snapshot = new MapAoiSnapshotPb { MapId = layout.MapId,
            Layout = new() { Width = layout.Width, Height = layout.Height, TileSize = layout.TileSize, ChunkSizeTiles = layout.ChunkSizeTiles },
            Center = new() { X = cx, Y = cy } };
        var chunks = new Dictionary<(int, int), MapChunkPb>();
        for (var y = Math.Max(0, cy - 1); y <= Math.Min(rows - 1, cy + 1); y++)
        for (var x = Math.Max(0, cx - 1); x <= Math.Min(columns - 1, cx + 1); x++)
        {
            var chunk = new MapChunkPb { Coordinate = new() { X = x, Y = y } };
            chunks.Add((x, y), chunk); snapshot.Chunks.Add(chunk);
        }
        var ids = new HashSet<Guid>();
        foreach (var data in context.Cache.Objects.Value)
        {
            if (data is null || data.ObjectId == Guid.Empty || !ids.Add(data.ObjectId) || data.Revision <= 0 ||
                !float.IsFinite(data.X) || !float.IsFinite(data.Y) || data.X < 0 || data.Y < 0 ||
                data.X >= (long)layout.Width * layout.TileSize || data.Y >= (long)layout.Height * layout.TileSize)
                throw new InvalidDataException("地图对象身份或位置无效");
            var monster = !string.IsNullOrWhiteSpace(data.MonsterTypeId);
            if (monster ? data.ItemId != 0 || data.Count != 0 : data.MonsterTypeId is not null || data.ItemId <= 0 || data.Count <= 0 || !context.Config.Item.Items.TryGetValue(data.ItemId, out var definition) || data.Count > definition.MaxStack)
                throw new InvalidDataException("地图对象载荷无效");
            if (!chunks.TryGetValue(((int)(data.X / size), (int)(data.Y / size)), out var chunk)) continue;
            var message = new MapObjectPb { ObjectId = data.ObjectId.ToString("N"), Revision = data.Revision, X = data.X, Y = data.Y };
            if (monster) message.Monster = new() { MonsterTypeId = data.MonsterTypeId };
            else message.GroundItem = new() { ItemId = data.ItemId, Count = data.Count };
            chunk.Objects.Add(message);
        }
        return snapshot;
    }
}
