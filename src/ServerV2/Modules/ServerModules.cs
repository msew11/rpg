using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Role.Storage;
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
    internal ServerModules(IAccountRoleSummaryRepo summaries, IRoleRepo roles, Func<string, ActorHandlers<LoginContext>> login,
        Func<long, ActorHandlers<RoleContext>>? role = null, Func<int, ActorHandlers<WorldContext>>? world = null,
        Action<Exception>? report = null)
    {
        _login = new(key => new LoginActor(key, summaries, login(key)), report);
        _role = new(id => new RoleActor(id, roles, role?.Invoke(id) ?? new()), report);
        _world = new(id => new WorldActor(id, world?.Invoke(id) ?? new()), report);
    }
    /// <summary>规范化后定位账号分片，不预加载其他账号。</summary>
    internal ActorRef Login(string username) => _login.Get(UsernameKey.Normalize(username));
    /// <summary>仅允许合法的全局角色身份。</summary>
    internal ActorRef Role(long roleId) => roleId > 0 ? _role.Get(roleId) : throw new ArgumentOutOfRangeException(nameof(roleId));
    /// <summary>仅允许合法地图身份。</summary>
    internal ActorRef World(int mapId) => mapId > 0 ? _world.Get(mapId) : throw new ArgumentOutOfRangeException(nameof(mapId));
    /// <summary>先角色后账号，同组并行；错误汇总且不阻止其他分片保存。</summary>
    internal async Task FlushAsync()
    {
        var errors = new List<Exception>();
        errors.AddRange(await FlushGroupAsync(_role.StartedRefs()).ConfigureAwait(false));
        errors.AddRange(await FlushGroupAsync(_login.StartedRefs()).ConfigureAwait(false));
        if (errors.Count != 0) throw new AggregateException("V2 分片保存失败", errors);
    }
    /// <summary>独立分片各自返回保存结果，不让一次失败短路整组。</summary>
    private static async Task<Exception[]> FlushGroupAsync(ActorRef[] actors)
    {
        var errors = await Task.WhenAll(actors.Select(async actor =>
        {
            try { await actor.Ask(new DataFlush()).ConfigureAwait(false); return null; }
            catch (Exception error) { return error; }
        })).ConfigureAwait(false);
        return errors.OfType<Exception>().ToArray();
    }
    /// <summary>先停止角色，再停止其下游地图与账号；模块不得形成循环 Ask。</summary>
    public async ValueTask DisposeAsync()
    {
        await _role.StopAsync().ConfigureAwait(false);
        await _world.StopAsync().ConfigureAwait(false);
        await _login.StopAsync().ConfigureAwait(false);
    }
}
