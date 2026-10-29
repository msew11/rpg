using System.Threading.Tasks;
using Rpg.ServerV2.ActorProtocol;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.World.Handler;

/// <summary>只读取所属地图完整对象快照，不执行 IO 或修改保存基线。</summary>
public sealed class GetWorldObjectCountHandler : IActorHandler<WorldContext, GetWorldObjectCount, GetWorldObjectCountRt>
{
    /// <summary>直接返回内部协议回复，返回后不保留或修改该对象。</summary>
    public ValueTask<GetWorldObjectCountRt> HandleAsync(WorldContext context, GetWorldObjectCount request) =>
        ValueTask.FromResult(new GetWorldObjectCountRt { Count = context.Objects.Length });
}
