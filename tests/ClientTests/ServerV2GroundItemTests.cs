using Google.Protobuf;
using Rpg.Config;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Connections.Data;
using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Modules.Role.Storage;
using Rpg.ServerV2.Modules.World.Storage;
using Rpg.ServerV2.Modules.World.Data;
using Codec = Rpg.ServerV2.Protocol.MessageCodec;

namespace ClientTests;

/// <summary>对应 server-v2-ground-items：立即回复、竞争、静默取消、最新容量及共同恢复。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2GroundItemTests : IDisposable
{
    /// <summary>本例独占存档。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-v2-ground-" + Guid.NewGuid());
    /// <summary>关联请求编号。</summary>
    private ulong _request;
    /// <summary>两个角色竞争同一地面物品，只一份成功结果和持有，静止观察者收到对象增删。</summary>
    [Fact]
    public async Task CompetingPickupsHaveOneWinnerAndOrderedFinalNotice()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        await using var server = MockServerService.CreateInProcess(env, config);
        var errors = new List<Exception>(); server.Error += errors.Add;
        var one = server.OpenConnection(); var two = server.OpenConnection();
        var first = await Enter(server, one, "demo1"); var second = await Enter(server, two, "demo2");
        await Exchange(server, one, new Gm { Command = "item 1012" });
        var drop = Assert.IsType<DropItemRt>((await Exchange(server, one, new DropItem { InstanceId = 1, Count = 1,
            DropX = first.State.Position.X + 100, DropY = first.State.Position.Y + 100 })).Last());
        Assert.False(File.Exists(Path.Combine(_root, "v2", "maps", "1", "manifest.json")));
        await Task.Delay(110); await server.Server!.AdvanceAsync();
        Assert.Contains(ReadAll(server, two).OfType<WorldUpdateNotice>(), notice => notice.Frame.Delta?.Map?.UpsertedObjects.Any(item => item.ObjectId == drop.ObjectId) == true);
        var point = new MoveTargetPb { X = first.State.Position.X + 100, Y = first.State.Position.Y + 100 };
        foreach (var id in new[] { one, two })
        {
            await Exchange(server, id, new Move { ActionId = 1, Target = point.Clone() });
            var accepted = await Exchange(server, id, new PickupItem { ObjectId = drop.ObjectId, MoveActionId = 1 });
            Assert.IsType<PickupItemRt>(Assert.Single(accepted));
        }
        server.Server.World!.Speed = 100000; await server.Server.AdvanceAsync(); await Task.Delay(5); await server.Server.AdvanceAsync();
        var outputs = new[] { ReadAll(server, one), ReadAll(server, two) };
        var results = outputs.SelectMany(list => list).OfType<PickupItemResultNotice>().ToArray();
        Assert.True(results.Length == 2, string.Join("\n", errors.Select(error => error.ToString())) + " outputs=" + string.Join(" | ", outputs.SelectMany(list => list))); Assert.Single(results.Where(result => result.Success));
        var winner = outputs.Single(list => list.OfType<PickupItemResultNotice>().Any(result => result.Success));
        Assert.True(winner.FindIndex(item => item is RoleItemsChangedNotice) < winner.FindIndex(item => item is PickupItemResultNotice));
        await Task.Delay(110); await server.Server.AdvanceAsync();
        Assert.Contains(new[] { one, two }.SelectMany(id => ReadAll(server, id)).OfType<WorldUpdateNotice>(),
            notice => notice.Frame.Delta?.Map?.RemovedObjectIds.Contains(drop.ObjectId) == true);
        await server.FlushAsync();
        var repo = new RepoRole(Path.Combine(_root, "v2"));
        Assert.Equal(1, (await repo.LoadAsync(first.State.RoleId)).Items.Length + (await repo.LoadAsync(second.State.RoleId)).Items.Length);
        var map = config.Map.Items[1];
        Assert.Empty((await new RepoWorld(Path.Combine(_root, "v2")).LoadAsync(new(1, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles))).Objects);
        Assert.Equal(new[] { "role_id", "object_id", "move_action_id", "success" }, PickupItemResultNotice.Descriptor.Fields.InDeclarationOrder().Select(field => field.Name));
    }
    /// <summary>走近中背包改变继续到达并按最新容量失败；新移动静默取消，没有取消结果。</summary>
    [Fact]
    public async Task BagChangesDoNotCancelButNewMovementCancelsSilently()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var id = server.OpenConnection(); var entry = await Enter(server, id, "demo1");
        await Exchange(server, id, new Gm { Command = "item 1012" });
        var dropped = Assert.IsType<DropItemRt>((await Exchange(server, id, new DropItem { InstanceId = 1, Count = 1,
            DropX = entry.State.Position.X + 100, DropY = entry.State.Position.Y + 100 })).Last());
        var target = new MoveTargetPb { X = entry.State.Position.X + 100, Y = entry.State.Position.Y + 100 };
        await Exchange(server, id, new Move { ActionId = 1, Target = target.Clone() });
        await Exchange(server, id, new PickupItem { ObjectId = dropped.ObjectId, MoveActionId = 1 });
        await Exchange(server, id, new Gm { Command = "item 1012 40" });
        server.Server!.World!.Speed = 100000; await server.Server.AdvanceAsync(); await Task.Delay(5); await server.Server.AdvanceAsync();
        Assert.False(Assert.Single(ReadAll(server, id).OfType<PickupItemResultNotice>()).Success);
        await Exchange(server, id, new Move { ActionId = 2, Target = target.Clone() });
        await Exchange(server, id, new PickupItem { ObjectId = dropped.ObjectId, MoveActionId = 2 });
        await Exchange(server, id, new Move { ActionId = 3, Target = new() { X = target.X + 500, Y = target.Y } });
        await server.Server.AdvanceAsync();
        Assert.Empty(ReadAll(server, id).OfType<PickupItemResultNotice>());
        var removed = await Exchange(server, id, new DropItem { InstanceId = 2, Count = 1, Destroy = true });
        Assert.Equal("", Assert.IsType<DropItemRt>(removed.Last()).ObjectId);
        Assert.Equal(39, Assert.IsType<RoleItemsChangedNotice>(removed[0]).Bag.Items.Count);
    }
    /// <summary>控制解除及接管静默取消待拾取，重新进入不恢复旧意图。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeavingOrTakingOverCancelsPendingPickup(bool takeover)
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var id = server.OpenConnection(); var entry = await Enter(server, id, "demo1");
        await Exchange(server, id, new Gm { Command = "item 1012" });
        var dropped = Assert.IsType<DropItemRt>((await Exchange(server, id, new DropItem { InstanceId = 1, Count = 1,
            DropX = entry.State.Position.X + 100, DropY = entry.State.Position.Y + 100 })).Last());
        await Exchange(server, id, new Move { ActionId = 1, Target = new() { X = entry.State.Position.X + 100, Y = entry.State.Position.Y + 100 } });
        await Exchange(server, id, new PickupItem { ObjectId = dropped.ObjectId, MoveActionId = 1 });
        var current = id;
        if (takeover)
        {
            current = server.OpenConnection(); await Exchange(server, current, new Login { Username = "demo1", Password = "123" });
        }
        else await Exchange(server, id, new LeaveWorld());
        var returned = Assert.IsType<EnterWorldRt>((await Exchange(server, current, new EnterWorld { RoleId = entry.State.RoleId })).Last());
        server.Server!.World!.Speed = 100000; await server.Server.AdvanceAsync(); await Task.Delay(5); await server.Server.AdvanceAsync();
        Assert.Empty(ReadAll(server, current).OfType<PickupItemResultNotice>());
        if (takeover) Assert.Empty(ReadAll(server, id).OfType<PickupItemResultNotice>());
        Assert.Empty(returned.Bag.Items); Assert.Single(returned.Map.Chunks.SelectMany(chunk => chunk.Objects));
    }

    /// <summary>生产宿主启动在登录和角色读取前恢复中断批次，避免背包与地图分叉。</summary>
    [Fact]
    public async Task RestartRecoversInterruptedPickupBeforeAnyBusinessRead()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        long roleId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var id = server.OpenConnection(); var entry = await Enter(server, id, "demo1"); roleId = entry.State.RoleId;
            await Exchange(server, id, new Gm { Command = "item 1012" });
            await Exchange(server, id, new DropItem { InstanceId = 1, Count = 1 });
            await server.FlushAsync();
        }
        var root = Path.Combine(_root, "v2"); var roles = new RepoRole(root); var worlds = new RepoWorld(root);
        var map = config.Map.Items[1]; var layout = new DataWorldLayout(1, map.Width, map.Height, map.TileSize, map.ChunkSizeTiles);
        var original = await roles.LoadAsync(roleId); var ground = Assert.Single((await worlds.LoadAsync(layout)).Objects);
        var next = original.Role.NextInstanceId;
        var gained = original with { Role = original.Role with { NextInstanceId = next + 1 }, Items = [new(next, 1012, 0, 1)], Equipments = [new() { InstanceId = next }] };
        var batch = new Rpg.ServerV2.Modules.Common.Storage.RepoItemBatch(root, roles, worlds, new Rpg.ServerV2.Modules.Login.Storage.RepoAccountRoleSummary(root))
        { AfterWrite = stage => { if (stage == "world") throw new IOException("模拟中断"); } };
        await Assert.ThrowsAsync<IOException>(() => batch.SaveAsync([new(roleId, gained, Rpg.ServerV2.Modules.Role.Data.DataRoleTables.All)],
            [new(layout, new([]), System.Collections.Immutable.ImmutableHashSet.Create(layout.ChunkAt(ground)))], []).AsTask());
        await using var restarted = MockServerService.CreateInProcess(env, config);
        var connection = restarted.OpenConnection(); await Exchange(restarted, connection, new Login { Username = "demo1", Password = "123" });
        var restored = Assert.IsType<EnterWorldRt>((await Exchange(restarted, connection, new EnterWorld { RoleId = roleId })).Last());
        Assert.Empty(restored.Bag.Items); Assert.Equal(ground.ObjectId.ToString("N"), Assert.Single(restored.Map.Chunks.SelectMany(chunk => chunk.Objects)).ObjectId);
        Assert.False(File.Exists(Path.Combine(root, "items.pending.json")));
    }

    /// <summary>真实账号链路建立角色和初始世界。</summary>
    private async Task<EnterWorldRt> Enter(MockServerService server, DataConnectionId id, string username)
    {
        await Exchange(server, id, new Login { Username = username, Password = "123" });
        var role = Assert.Single(Assert.IsType<CreateRoleRt>((await Exchange(server, id, new CreateRole { Name = username, Gender = RoleGenderPb.Male, HeadId = 1 })).Last()).Roles);
        return Assert.IsType<EnterWorldRt>((await Exchange(server, id, new EnterWorld { RoleId = role.RoleId })).Last());
    }
    /// <summary>保留本次推送并在关联回复后返回。</summary>
    private async Task<List<IMessage>> Exchange(MockServerService server, DataConnectionId id, IMessage request)
    {
        ReadAll(server, id); var number = ++_request;
        await server.ReceiveAsync(id, Codec.Encode(request, server.Session(id).Generation, number, true)).WaitAsync(TimeSpan.FromSeconds(5));
        var result = new List<IMessage>();
        while (server.TryReadOutgoing(id, out var bytes))
        { var envelope = Codec.ReadEnvelope(bytes); result.Add(Codec.ReadPayload(envelope, false)); if (envelope.RequestId == number) return result; }
        throw new InvalidOperationException("请求未回复");
    }
    /// <summary>排空已有推送。</summary>
    private static List<IMessage> ReadAll(MockServerService server, DataConnectionId id)
    { var result = new List<IMessage>(); while (server.TryReadOutgoing(id, out var bytes)) result.Add(Codec.ReadPayload(Codec.ReadEnvelope(bytes), false)); return result; }
    /// <summary>清理独占文件。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
