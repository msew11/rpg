using Rpg.Server.Modules;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Rpg.Config;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules.Common;
using Rpg.Server.Modules.Role.Async;
using Rpg.Server.Modules.World.Handler;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.World;
using Rpg.Server.Modules.World.Storage;
using Rpg.Server.Runtime;

namespace ClientTests;

/// <summary>服务于 server-runtime、server-storage 与 server-network 契约的受控测试模块组合，仅在测试侧替换处理器。</summary>
internal sealed class DoubleServerModules : IServerModules
{
    /// <summary>自定义处理器表不隐式添加多人业务。</summary>
    public bool SupportsMovement { get; }
    /// <summary>各分片共用本实例的运行时身份校验目录。</summary>
    public ActorCallRegistry Calls { get; } = new();
    /// <summary>装配入口确定的统一配置，所有分片必须借用此实例。</summary>
    public ConfigService Config { get; }
    /// <summary>按规范账号键的分片目录。</summary>
    private readonly ActorDirectory<string> _login;
    /// <summary>按全局角色身份的分片目录。</summary>
    private readonly ActorDirectory<long> _role;
    /// <summary>按地图身份的分片目录。</summary>
    private readonly ActorDirectory<int> _world;

    /// <summary>每次创建 Actor 时构造专用注册表，工厂不在消息中流转。</summary>
    internal DoubleServerModules(
        ConfigService config,
        IAccountRoleSummaryRepo summaries,
        IRoleRepo roles,
        IWorldRepo worlds,
        Func<string, ActorHandlers<LoginContext>> login,
        Func<long, ActorHandlers<RoleContext>>? role = null,
        Func<int, ActorHandlers<WorldContext>>? world = null,
        Action<Exception>? report = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(summaries);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(worlds);
        ArgumentNullException.ThrowIfNull(login);
        ObjectDisposedException.ThrowIf(config.IsDisposed, config);
        SupportsMovement = role is null && world is null;
        role ??= static _ => new ActorHandlers<RoleContext>().Register(new GetRoleWorldObjectCountHandler())
            .Register(new PrepareRoleHandler())
            .Register(new MoveHandler())
            .Register(new AdvanceRoleHandler())
            .Register(new DropItemHandler())
            .Register(new EquipItemHandler())
            .Register(new CompleteRolePickupHandler())
            .Register(new Rpg.Server.Modules.Role.Handler.GmHandler())
            .Register(new Rpg.Server.Modules.Role.Handler.MoveItemHandler())
            .Register(new Rpg.Server.Modules.Role.Handler.GetRoleItemsHandler())
            .Register(new Rpg.Server.Modules.Role.Handler.PickupItemHandler())
            ;
        world ??= static _ => new ActorHandlers<WorldContext>().Register(new GetWorldObjectCountHandler())
            .Register(new GetWorldEntrySnapshotHandler())
            .Register(new UpdateWorldRoleHandler())
            .Register(new BuildWorldUpdatesHandler())
            .Register(new GetWorldSnapshotHandler())
            .Register(new CreateGroundItemHandler())
            .Register(new GetGroundItemHandler())
            .Register(new RemoveGroundItemHandler())
            ;
        report ??= static _ => { };
        Config = config;
        _login = new(key => new LoginActor(config, this, key, summaries, login(key)) { Calls = Calls }, report);
        _role = new(id => new RoleActor(config, this, id, roles, role(id)) { Calls = Calls }, report);
        _world = new(id => new WorldActor(config, this, id, worlds, world(id)) { Calls = Calls }, report);
    }
    /// <summary>规范化后定位账号分片，不预加载其他账号。</summary>
    public ActorRef Login(string username) => _login.Get(UsernameKey.Normalize(username));
    /// <summary>仅允许合法的全局角色身份。</summary>
    public ActorRef Role(long roleId) => roleId > 0 ? _role.Get(roleId) : throw new ArgumentOutOfRangeException(nameof(roleId));
    /// <summary>仅允许合法地图身份。</summary>
    public ActorRef World(int mapId) => mapId > 0 ? _world.Get(mapId) : throw new ArgumentOutOfRangeException(nameof(mapId));
    /// <summary>进入前只在已发生地面转移时提交共同批次。</summary>
    public bool GroundItemsPending { get; set; }
    /// <summary>按角色、地图、账号保存，同组并行；错误汇总且不阻止其他分片保存。</summary>
    public async Task FlushAsync()
    {
        var errors = new List<Exception>();
        errors.AddRange(await FlushGroupAsync(_role.StartedRefs()).ConfigureAwait(false));
        errors.AddRange(await FlushGroupAsync(_world.StartedRefs()).ConfigureAwait(false));
        errors.AddRange(await FlushGroupAsync(_login.StartedRefs()).ConfigureAwait(false));
        if (errors.Count != 0) throw new AggregateException("分片保存失败", errors);
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
