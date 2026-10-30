using System.Threading.Tasks;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.World.Utils;
using Rpg.Server.Runtime;

namespace Rpg.Server.Modules.World.Handler;

/// <summary>生成指定观察者的完整世界帧。</summary>
internal sealed class GetWorldSnapshotHandler : IActorHandler<WorldContext, GetWorldSnapshot, GetWorldSnapshotRt>
{
    /// <summary>全量重同步与普通发布共用同一序号。</summary>
    public ValueTask<GetWorldSnapshotRt> HandleAsync(WorldContext context, GetWorldSnapshot request) =>
        ValueTask.FromResult(new GetWorldSnapshotRt { Frame = WorldFrames.Build(context, request.RoleId, request.ServerTimeMs, true) });
}
