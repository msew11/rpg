using Rpg.Config;
using Rpg.ServerV2.Modules.Common;
using Rpg.ServerV2.Modules.Login.Cache;
using Rpg.ServerV2.Modules.Login.Data;

namespace Rpg.ServerV2.Modules.Login;

/// <summary>账号处理器的服务入口，仅引用所属工作区，不管理数据或保存基线。</summary>
public sealed class LoginContext : ModuleContext
{
    /// <summary>所属邮箱独占的可变数据工作区。</summary>
    public LoginCache Cache { get; }
    /// <summary>完整加载后建立工作区，借用基类配置及 Actor 定位服务。</summary>
    internal LoginContext(ConfigService config, IActorLocator actors, string username, DataLoginSnapshot snapshot) : base(config, actors)
        => Cache = new(username, snapshot);
}
