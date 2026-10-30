using Rpg.Config;
using Rpg.Server.Modules.Common;
using Rpg.Server.Modules.Role.Cache;
using Rpg.Server.Modules.Role.Data;

namespace Rpg.Server.Modules.Role;

/// <summary>角色处理器的服务入口，仅引用所属工作区，不管理数据或保存基线。</summary>
public sealed class RoleContext : ModuleContext
{
    /// <summary>所属邮箱独占的可变数据工作区。</summary>
    public RoleCache Cache { get; }
    /// <summary>完整加载后建立工作区，借用基类配置及 Actor 定位服务。</summary>
    internal RoleContext(ConfigService config, IActorLocator actors, long roleId, DataRoleSnapshot? snapshot) : base(config, actors)
        => Cache = new(roleId, snapshot);
}
