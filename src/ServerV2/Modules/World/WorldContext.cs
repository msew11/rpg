using Rpg.Config;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.World.Cache;
using Rpg.ServerV2.Modules.World.Data;

namespace Rpg.ServerV2.Modules.World;

/// <summary>地图处理器的服务入口，仅引用所属工作区，不管理数据或保存基线。</summary>
public sealed class WorldContext : ModuleContext
{
    /// <summary>所属邮箱独占的可变数据工作区。</summary>
    public WorldCache Cache { get; }
    /// <summary>完整加载后建立工作区，借用基类配置及 Actor 定位服务。</summary>
    internal WorldContext(ConfigService config, IActorLocator actors, DataWorldLayout layout, DataWorldSnapshot snapshot) : base(config, actors)
        => Cache = new(layout, snapshot);
}
