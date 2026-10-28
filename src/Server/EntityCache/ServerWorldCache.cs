using System.Collections.Generic;

namespace Rpg.Server.EntityCache;

/// <summary>单个地图 Actor 独占的地图状态与共同提交关系。</summary>
public sealed class ServerWorldCache
{
    /// <summary>初始化后的唯一可写地图。</summary>
    public ServerMap Map { get; }
    /// <summary>与本地图共同落地的角色身份。</summary>
    internal HashSet<long> Transfers { get; } = new();

    /// <summary>绑定本地图独立实例。</summary>
    public ServerWorldCache(ServerMap map) { Map = map; }
}
