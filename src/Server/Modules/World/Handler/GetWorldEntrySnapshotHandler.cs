using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>从完整地图缓存投影初始九块，空块也返回且不改变保存基线。</summary>
public sealed class GetWorldEntrySnapshotHandler : IActorHandler<WorldContext, GetWorldEntrySnapshot, GetWorldEntrySnapshotRt>
{
    /// <summary>只按权威位置和配置裁剪范围，不注册成员或订阅。</summary>
    public ValueTask<GetWorldEntrySnapshotRt> HandleAsync(WorldContext context, GetWorldEntrySnapshot request)
        => ValueTask.FromResult(new GetWorldEntrySnapshotRt { Map = WorldEntrySnapshot.Build(context, request.X, request.Y) });
}
