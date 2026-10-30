using System.Collections.Immutable;
using Google.Protobuf;
using Rpg.Config;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Connections.Data;
using Rpg.Server.Hosting;
using Rpg.Server.Modules;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Login.Handler;
using Rpg.Server.Modules.Login.Async;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Storage;
using Rpg.Server.Modules.World;
using Rpg.Server.Modules.World.Handler;
using Rpg.Server.Modules.World.Storage;
using Rpg.Server.Protocol;
using Rpg.Server.Runtime;

namespace ClientTests;

/// <summary>真实字节和文件验收 server-world-entry 的可信建档、恢复及绑定隔离。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerWorldEntryTests : IDisposable
{
    /// <summary>用例独占目录，不触碰玩家存档。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-entry-" + Guid.NewGuid());
    /// <summary>请求关联编号。</summary>
    private ulong _request;
    /// <summary>独立 数据根。</summary>
    private string Root => Path.GetFullPath(_directory);

    /// <summary>全缺失才首次建档，男女初始值、重复进入、切角、多连接及重启均保持只读。</summary>
    [Fact]
    public async Task FirstEntrySwitchAndRestartKeepThreeTablesAndNoMapWrites()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        long male; long female;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var one = server.OpenConnection();
            await Login(server, one);
            male = await Create(server, one, "男角色");
            female = await Create(server, one, "女角色", RoleGenderPb.Female);
            Assert.False(File.Exists(Path.Combine(Root, "demo1.role_summary.json")));
            var first = Assert.IsType<EnterWorldRt>(await Exchange(server, one, new EnterWorld { RoleId = male }));
            Assert.Equal(male, server.Session(one).RoleId);
            Assert.Equal(100, first.State.Status.Health);
            Assert.Equal(60, first.State.Status.Mana);
            Assert.Equal(500, first.Role.NextExperience);
            Assert.Empty(first.Bag.Items);
            Assert.Equal(9, first.Map.Chunks.Count);
            Assert.All(first.Map.Chunks, chunk => Assert.Empty(chunk.Objects));
            Assert.Equal(16016, first.State.Position.X);
            Assert.Equal(6, Directory.GetFiles(Root).Length);
            Assert.Empty(Directory.GetDirectories(Root));
            var same = Assert.IsType<EnterWorldRt>(await Exchange(server, one, new EnterWorld { RoleId = male }));
            Assert.Equal(first, same);
            var second = Assert.IsType<EnterWorldRt>(await Exchange(server, one, new EnterWorld { RoleId = female }));
            Assert.Equal(16144, second.State.Position.X);
            Assert.Equal(16080, second.State.Position.Y);
            Assert.Equal(female, server.Session(one).RoleId);
            var two = server.OpenConnection();
            await Login(server, two);
            Assert.Equal(second, Assert.IsType<EnterWorldRt>(await Exchange(server, two, new EnterWorld { RoleId = female })));
            var generation = server.ResetSession(one);
            Assert.Equal(0, server.Session(one).RoleId);
            await Exchange(server, one, new Rpg.Protocol.Login { Username = "demo1", Password = "123" }, generation);
            Assert.Equal(ErrorCodePb.RoleNotFound, Assert.IsType<ErrorRt>(await Exchange(server, one, new EnterWorld { RoleId = 99 }, generation)).Code);
            Assert.Equal(0, server.Session(one).RoleId);
            await Exchange(server, one, new Rpg.Protocol.Login { Username = "demo1", Password = "wrong" }, generation);
            Assert.Equal(0, server.Session(one).RoleId);
        }
        var repo = new RepoRole(Root);
        var saved = await repo.LoadAsync(male);
        Assert.Equal(1, saved.Role.NextInstanceId);
        await repo.SaveAsync(male, saved with { Role = saved.Role with { Health = 37, X = 0, Y = 0, Level = 4, Name = "存档名称" } }, DataRoleTables.Role);
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            var entered = Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = male }));
            Assert.Equal(37, entered.State.Status.Health);
            Assert.Equal(0, entered.State.Position.X);
            Assert.Equal(4, entered.Map.Chunks.Count);
            var login = Assert.IsType<LoginRt>(await Exchange(server, id, new Rpg.Protocol.Login { Username = "demo1", Password = "123" }));
            Assert.Equal("存档名称", login.Roles.Single(role => role.RoleId == male).Name);
            Assert.Equal(4, login.Roles.Single(role => role.RoleId == male).Level);
        }
        Assert.Equal(9, Directory.GetFiles(Root).Length);
        Assert.Empty(Directory.GetDirectories(Root));
    }

    /// <summary>已有装备实例和动态地图按全量私有/公开分离交付，进入与 Flush 不改写三表或地图。</summary>
    [Fact]
    public async Task ExistingItemsAndDynamicObjectsAreProjectedWithoutStorageChanges()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var equipment = config.Item.Items.Values.First(item => item.MaxStack == 1 &&
            Rpg.Utils.EquipmentSlotUtil.TryGetSlot(item.Category, item.SubCategory, out var slot) && slot == Rpg.Const.EquipmentSlot.MainHand);
        long roleId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id); roleId = await Create(server, id, "已有实例");
            await Exchange(server, id, new EnterWorld { RoleId = roleId });
        }
        var repo = new RepoRole(Root);
        var saved = await repo.LoadAsync(roleId);
        await repo.SaveAsync(roleId, saved with { Role = saved.Role with { NextInstanceId = 3, Health = 19, Level = 7 },
            Items = [new DataItem(1, equipment.Id, Rpg.Const.EquipmentSlot.MainHand), new DataItem(2, equipment.Id, 0, 1)],
            Equipments = [new() { InstanceId = 1 }, new() { InstanceId = 2 }] }, DataRoleTables.All);
        var map = config.Map.Items[config.Basic.Value.InitialMapId];
        var layout = new Rpg.Server.Modules.World.Data.DataWorldLayout(map.Id, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles);
        var itemObject = new Rpg.Server.Modules.World.Data.DataWorldObject { ObjectId = Guid.NewGuid(), Revision = 3,
            X = saved.Role.X, Y = saved.Role.Y, ItemId = equipment.Id, Count = 1 };
        var monster = itemObject with { ObjectId = Guid.NewGuid(), Revision = 8, ItemId = 0, Count = 0, MonsterTypeId = "演示怪物" };
        await new RepoWorld(Root).SaveAsync(layout, new([itemObject, monster]), ImmutableHashSet.Create(layout.ChunkAt(itemObject)));
        var files = Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(path => !path.EndsWith("role_summary.json"))
            .ToDictionary(path => path, File.ReadAllBytes);
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            var reply = Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId }));
            Assert.Equal(new[] { equipment.Id }, reply.State.Equipment.EquippedItems);
            Assert.Equal(2, reply.Bag.Items.Count);
            Assert.All(reply.Bag.Items, item => Assert.NotNull(item.Equipment));
            Assert.True(reply.Bag.Items.Single(item => item.InstanceId == 1).Equipped);
            var objects = reply.Map.Chunks.SelectMany(chunk => chunk.Objects).ToArray();
            Assert.Equal(2, objects.Length);
            Assert.Equal(3, objects.Single(value => value.ObjectId == itemObject.ObjectId.ToString("N")).Revision);
            Assert.Equal("演示怪物", objects.Single(value => value.Monster is not null).Monster.MonsterTypeId);
            await server.FlushAsync();
            var summary = (await new RepoAccountRoleSummary(Root).LoadAsync("demo1")).Single();
            Assert.Equal(7, summary.Level); Assert.Equal(new[] { equipment.Id }, summary.EquippedItems);
        }
        foreach (var (path, bytes) in files) Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    /// <summary>存储层允许原始值，进入业务拒绝无效身份、位置及物品关联，不覆盖已加载文件。</summary>
    [Theory]
    [InlineData("identity")]
    [InlineData("position")]
    [InlineData("equipment")]
    public async Task InvalidStoredBusinessDataIsRejectedWithoutOverwrite(string fault)
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        long roleId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id); roleId = await Create(server, id, "非法存档");
            await Exchange(server, id, new EnterWorld { RoleId = roleId });
        }
        var repo = new RepoRole(Root); var original = await repo.LoadAsync(roleId);
        var broken = fault switch {
            "identity" => original with { Role = original.Role with { RoleId = roleId + 1 } },
            "position" => original with { Role = original.Role with { MapId = int.MaxValue } },
            _ => original with { Equipments = [new() { InstanceId = 9 }] }
        };
        await repo.SaveAsync(roleId, broken, DataRoleTables.All);
        var files = Directory.GetFiles(Root).ToDictionary(path => path, File.ReadAllBytes);
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            Assert.Equal(ErrorCodePb.InternalError, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId })).Code);
        }
        foreach (var (path, bytes) in files) Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        await repo.SaveAsync(roleId, original, DataRoleTables.All);
        await using (var repaired = MockServerService.CreateInProcess(env, config))
        {
            var id = repaired.OpenConnection(); await Login(repaired, id);
            Assert.IsType<EnterWorldRt>(await Exchange(repaired, id, new EnterWorld { RoleId = roleId }));
        }
    }

    /// <summary>地图存储允许原始数量，进入业务须拒绝无法接纳的地面堆叠，修复存档重启后可恢复。</summary>
    [Fact]
    public async Task InvalidGroundStackFailsBeforeConnectionBinding()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var definition = config.Item.Items.Values.First(item => item.MaxStack == 1);
        long roleId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id); roleId = await Create(server, id, "地图数量");
            await Exchange(server, id, new EnterWorld { RoleId = roleId });
        }
        var data = (await new RepoRole(Root).LoadAsync(roleId)).Role;
        var map = config.Map.Items[data.MapId];
        var layout = new Rpg.Server.Modules.World.Data.DataWorldLayout(map.Id, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles);
        var ground = new Rpg.Server.Modules.World.Data.DataWorldObject { ObjectId = Guid.NewGuid(), Revision = 1,
            X = data.X, Y = data.Y, ItemId = definition.Id, Count = 2 };
        var repo = new RepoWorld(Root);
        await repo.SaveAsync(layout, new([ground]), ImmutableHashSet.Create(layout.ChunkAt(ground)));
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            Assert.Equal(ErrorCodePb.InternalError, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId })).Code);
            Assert.Equal(0, server.Session(id).RoleId);
        }
        await repo.SaveAsync(layout, new([ground with { Count = 1 }]), ImmutableHashSet.Create(layout.ChunkAt(ground)));
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId }));
        }
    }

    /// <summary>认证、编号、归属和墓碑在角色定位前拒绝；摘要保存失败也不能留下三表。</summary>
    [Fact]
    public async Task RejectedOwnershipAndFailedSummarySaveNeverCreateRoleFiles()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var id = server.OpenConnection();
        Assert.Equal(ErrorCodePb.NotAuthenticated, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = 1003 })).Code);
        await Login(server, id);
        foreach (var roleId in new long[] { 0, -1, 99 })
            Assert.Equal(roleId > 0 ? ErrorCodePb.RoleNotFound : ErrorCodePb.InvalidRequest,
                Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId })).Code);
        var deleted = await Create(server, id, "墓碑");
        await Exchange(server, id, new DeleteRole { RoleId = deleted });
        Assert.Equal(ErrorCodePb.RoleNotFound, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = deleted })).Code);
        Assert.False(File.Exists(Path.Combine(Root, deleted + ".role.json")));
        var owned = await Create(server, id, "待保存");
        var blocked = Path.Combine(Root, "demo1.role_summary.json");
        Directory.CreateDirectory(blocked);
        Assert.Equal(ErrorCodePb.InternalError, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = owned })).Code);
        Assert.Equal(new[] { "role_id_sequence.json", "role_id_sequence.json.lock" },
            Directory.GetFiles(Root).Select(Path.GetFileName).Order());
        Directory.Delete(blocked);
        Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = owned }));
        Assert.False(File.Exists(Path.Combine(Root, deleted + ".role.json")));
    }

    /// <summary>部分缺失、损坏和 null 不覆盖源文件，修复后可重新初始化同角色。</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("broken")]
    public async Task PartialOrCorruptTablesRejectAndCanBeRepaired(string fault)
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        long roleId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            roleId = await Create(server, id, "修复角色");
            await Exchange(server, id, new EnterWorld { RoleId = roleId });
        }
        var path = Path.Combine(Root, roleId + ".item.json");
        if (fault == "missing") File.Delete(path);
        else await File.WriteAllTextAsync(path, fault == "null" ? "null" : "{");
        var roleBytes = await File.ReadAllBytesAsync(Path.Combine(Root, roleId + ".role.json"));
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            Assert.Equal(ErrorCodePb.InternalError, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId })).Code);
            Assert.Equal(roleBytes, await File.ReadAllBytesAsync(Path.Combine(Root, roleId + ".role.json")));
            if (fault != "missing") Assert.Equal(fault == "null" ? "null" : "{", await File.ReadAllTextAsync(path));
            else Assert.False(File.Exists(path));
            await File.WriteAllTextAsync(path, "[]");
            Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId }));
        }
    }

    /// <summary>首次多表保存中断保留完整候选，回滚后显式保存重试，周期禁用不影响首次落地。</summary>
    [Fact]
    public async Task InitialSaveFailureKeepsCandidateUntilFlushRetry()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        var interrupted = false;
        var fail = true;
        var repo = new RepoRole(Root) { AfterTableWritten = _ => { if (fail) { interrupted = true; throw new IOException("首次提交中断"); } } };
        await using var server = new MockServerService(env, Modules(config, repo));
        var id = server.OpenConnection(); await Login(server, id);
        var roleId = await Create(server, id, "候选保留");
        Assert.Equal(ErrorCodePb.InternalError, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId })).Code);
        Assert.True(interrupted);
        Assert.True(File.Exists(Path.Combine(Root, roleId + ".pending.json")));
        Assert.Equal(0, server.Session(id).RoleId);
        fail = false;
        await server.FlushAsync();
        Assert.False(File.Exists(Path.Combine(Root, roleId + ".pending.json")));
        var saved = await repo.LoadAsync(roleId);
        Assert.Empty(saved.Items); Assert.Empty(saved.Equipments);
        Assert.Equal("候选保留", saved.Role.Name);
        Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId }));
    }

    /// <summary>没有可信准备时定位、普通查询及 Flush 不会创建角色文件。</summary>
    [Fact]
    public async Task UninitializedRoleCannotBeCreatedByQueryOrFlush()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var modules = Modules(config, new RepoRole(Root));
        var actor = modules.Role(1003);
        await Assert.ThrowsAsync<InvalidOperationException>(() => actor.Ask<GetRoleWorldObjectCountRt>(new GetRoleWorldObjectCount()));
        await modules.FlushAsync();
        Assert.False(Directory.Exists(Root));
    }

    /// <summary>地图损坏不会覆盖既有三表，修复后重试；三表全部丢失则按首次重建。</summary>
    [Fact]
    public async Task MapFailureIsRepairableAndAllLostTablesRecreate()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var manifest = Path.Combine(Root, "maps", config.Basic.Value.InitialMapId.ToString(), "manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);
        await File.WriteAllTextAsync(manifest, "{");
        long roleId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id); roleId = await Create(server, id, "地图修复");
            Assert.Equal(ErrorCodePb.InternalError, Assert.IsType<ErrorRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId })).Code);
            var path = Path.Combine(Root, roleId + ".role.json");
            var original = await File.ReadAllBytesAsync(path);
            File.Delete(manifest);
            Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId }));
            Assert.Equal(original, await File.ReadAllBytesAsync(path));
        }
        foreach (var table in new[] { "role", "item", "equipment" }) File.Delete(Path.Combine(Root, roleId + "." + table + ".json"));
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); await Login(server, id);
            Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = roleId }));
        }
    }

    /// <summary>阻塞下游后切代次，旧结果不能替换此前成功绑定的角色或进入输出。</summary>
    [Fact]
    public async Task LateEntryCannotReplaceRoleAfterGenerationReset()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        var gate = new DoubleEntryGate();
        await using var server = new MockServerService(env, Modules(config, new RepoRole(Root), gate));
        var id = server.OpenConnection(); await Login(server, id);
        var first = await Create(server, id, "原角色");
        var second = await Create(server, id, "迟到角色");
        await Exchange(server, id, new EnterWorld { RoleId = first });
        gate.Block = true;
        var pending = server.ReceiveAsync(id, MessageCodec.Encode(new EnterWorld { RoleId = second }, 1, ++_request, true));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        server.ResetSession(id);
        gate.Release.SetResult();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(first, server.Session(id).RoleId);
        Assert.False(server.TryReadOutgoing(id, out _));
    }

    /// <summary>复用正式处理器，只允许注入文件故障或可控地图等待。</summary>
    private DoubleServerModules Modules(ConfigService config, IRoleRepo repo, DoubleEntryGate? gate = null) => new(config,
        new RepoAccountRoleSummary(Root), repo, new RepoWorld(Root), _ => new ActorHandlers<LoginContext>()
            .Register<Rpg.Protocol.Login, LoginRt>(new LoginHandler()).Register<CreateRole, CreateRoleRt>(new CreateRoleHandler())
            .Register<DeleteRole, DeleteRoleRt>(new DeleteRoleHandler()).Register<EnterWorld, EnterWorldRt>(new EnterWorldHandler()),
        world: gate is null ? null : _ => new ActorHandlers<WorldContext>().Register<GetWorldEntrySnapshot, GetWorldEntrySnapshotRt>(gate));
    /// <summary>可控等待服务正式世界快照 Handler，验证结果提交的代次边界。</summary>
    private sealed class DoubleEntryGate : IActorHandler<WorldContext, GetWorldEntrySnapshot, GetWorldEntrySnapshotRt>
    {
        /// <summary>仅后续进入需要阻塞。</summary>
        internal bool Block;
        /// <summary>通知测试请求已到达下游。</summary>
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>测试释放等待。</summary>
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>阻塞后仍执行真实快照投影。</summary>
        public async ValueTask<GetWorldEntrySnapshotRt> HandleAsync(WorldContext context, GetWorldEntrySnapshot request)
        {
            if (Block) { Entered.TrySetResult(); await Release.Task.ConfigureAwait(false); }
            return await new GetWorldEntrySnapshotHandler().HandleAsync(context, request).ConfigureAwait(false);
        }
    }
    /// <summary>真实信封的请求关联与代次验证。</summary>
    private async Task<IMessage> Exchange(MockServerService server, DataConnectionId id, IMessage request, ulong generation = 1)
    {
        var number = ++_request;
        await server.ReceiveAsync(id, MessageCodec.Encode(request, generation, number, true)).WaitAsync(TimeSpan.FromSeconds(5));
        byte[] bytes;
        do { Assert.True(server.TryReadOutgoing(id, out bytes)); } while (MessageCodec.ReadEnvelope(bytes).RequestId == 0);
        var envelope = MessageCodec.ReadEnvelope(bytes);
        Assert.Equal(number, envelope.RequestId); Assert.Equal(generation, envelope.Generation);
        return MessageCodec.ReadPayload(envelope, false);
    }
    /// <summary>使用演示凭据完成当前连接认证。</summary>
    private Task<IMessage> Login(MockServerService server, DataConnectionId id) => Exchange(server, id, new Rpg.Protocol.Login { Username = "demo1", Password = "123" });
    /// <summary>用真实摘要业务创建编号。</summary>
    private async Task<long> Create(MockServerService server, DataConnectionId id, string name, RoleGenderPb gender = RoleGenderPb.Male)
    {
        var reply = Assert.IsType<CreateRoleRt>(await Exchange(server, id, new CreateRole { Name = name, Gender = gender, HeadId = 17 }));
        return reply.Roles.Max(role => role.RoleId);
    }
    /// <summary>清理当前用例独占路径。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
