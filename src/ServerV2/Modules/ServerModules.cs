using System;
using System.Threading.Tasks;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Role;
using Rpg.ServerV2.Modules.World;
using Rpg.ServerV2.Runtime;

namespace Rpg.ServerV2.Modules;

/// <summary>持有三类分片目录，只负责模块装配和身份校验。</summary>
internal sealed class ServerModules : IAsyncDisposable
{
    /// <summary>按规范账号键的分片目录。</summary>
    private readonly ActorDirectory<string> _login;
    /// <summary>按全局角色身份的分片目录。</summary>
    private readonly ActorDirectory<long> _role;
    /// <summary>按地图身份的分片目录。</summary>
    private readonly ActorDirectory<int> _world;

    /// <summary>每次创建 Actor 时构造专用注册表，工厂不在消息中流转。</summary>
    internal ServerModules(Func<string, ActorHandlers<LoginContext>> login,
        Func<long, ActorHandlers<RoleContext>>? role = null, Func<int, ActorHandlers<WorldContext>>? world = null,
        Action<Exception>? report = null)
    {
        _login = new(key => new LoginActor(key, login(key)), report);
        _role = new(id => new RoleActor(id, role?.Invoke(id) ?? new()), report);
        _world = new(id => new WorldActor(id, world?.Invoke(id) ?? new()), report);
    }
    /// <summary>规范化后定位账号分片，不预加载其他账号。</summary>
    internal ActorRef Login(string username) => _login.Get(UsernameKey.Normalize(username));
    /// <summary>仅允许合法的全局角色身份。</summary>
    internal ActorRef Role(long roleId) => roleId > 0 ? _role.Get(roleId) : throw new ArgumentOutOfRangeException(nameof(roleId));
    /// <summary>仅允许合法地图身份。</summary>
    internal ActorRef World(int mapId) => mapId > 0 ? _world.Get(mapId) : throw new ArgumentOutOfRangeException(nameof(mapId));
    /// <summary>先停止角色，再停止其下游地图与账号；模块不得形成循环 Ask。</summary>
    public async ValueTask DisposeAsync()
    {
        await _role.StopAsync().ConfigureAwait(false);
        await _world.StopAsync().ConfigureAwait(false);
        await _login.StopAsync().ConfigureAwait(false);
    }
}
