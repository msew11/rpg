using System.Threading.Tasks;
using Rpg.ServerV2.ActorProtocol;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.Role.Async;

/// <summary>在角色同一邮箱回合中协调所属地图的只读对象数量查询。</summary>
public sealed class GetRoleWorldObjectCountHandler : IActorHandler<RoleContext, GetRoleWorldObjectCount, GetRoleWorldObjectCountRt>
{
    /// <summary>按当前 MapId 定位地图，直接传递内部协议；等待期间不让出角色回合。</summary>
    public async ValueTask<GetRoleWorldObjectCountRt> HandleAsync(RoleContext context, GetRoleWorldObjectCount request)
    {
        var reply = await context.Actors.World(context.Role.MapId)
            .Ask<GetWorldObjectCountRt>(new GetWorldObjectCount()).ConfigureAwait(false);
        return new GetRoleWorldObjectCountRt { Count = reply.Count };
    }
}
