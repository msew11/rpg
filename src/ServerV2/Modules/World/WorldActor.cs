using System.Threading.Tasks;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules.World;

/// <summary>按地图身份串行执行的模块，后续多个角色共用同一分片。</summary>
internal sealed class WorldActor : ModuleActor<WorldContext>
{
    /// <summary>本分片固定地图身份。</summary>
    private readonly int _mapId;
    /// <summary>绑定专用处理器表与地图身份。</summary>
    internal WorldActor(int mapId, ActorHandlers<WorldContext> handlers) : base(handlers) => _mapId = mapId;
    /// <summary>按需构造地图工作区，不创建旧版地图数据。</summary>
    protected override ValueTask<WorldContext> CreateContextAsync() => ValueTask.FromResult(new WorldContext(_mapId));
}
