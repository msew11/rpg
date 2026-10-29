using Rpg.Config;
using Rpg.ServerV2.Modules.Common;

namespace Rpg.ServerV2.Modules.World;

/// <summary>地图分片的身份边界，后续地图状态由本上下文独占。</summary>
public sealed class WorldContext : ModuleContext
{
    /// <summary>本地图的正整数身份。</summary>
    public int MapId { get; }
    /// <summary>由所属 Actor 初始化固定身份。</summary>
    internal WorldContext(ConfigService config, int mapId) : base(config) => MapId = mapId;
}
