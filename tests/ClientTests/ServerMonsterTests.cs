using Google.Protobuf;
using Rpg.Config;
using Rpg.Protocol;
using Rpg.Server.Connections.Data;
using Rpg.Server.Hosting;
using Codec = Rpg.Server.Protocol.MessageCodec;

namespace ClientTests;

/// <summary>对应 openspec/specs/gm-monster：权威创建、拒绝、静止多人 AOI 和重启恢复。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerMonsterTests : IDisposable
{
    /// <summary>独占服务器存档根。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-monster-server-" + Guid.NewGuid());
    /// <summary>真实信封的关联请求编号。</summary>
    private ulong _request;

    /// <summary>非法请求无副作用，创建仅回复 GM，两名静止观察者同步同一对象并恢复存档。</summary>
    [Fact]
    public async Task CreationRejectsInvalidRequestsBroadcastsAndSurvivesRestart()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root);
        using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        long roleId; MapObjectPb monster;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var one = server.OpenConnection(); var two = server.OpenConnection();
            Assert.IsType<ErrorRt>(Assert.Single(await Exchange(server, one, new Gm { Command = "monster 1001 16000 16000" })));
            var entry = await Enter(server, one, "demo1"); roleId = entry.State.RoleId;
            await Enter(server, two, "demo2");
            foreach (var command in new[] { "monster", "monster 1001 1", "monster unknown 16000 16000", "monster 1001 NaN 1",
                "monster 1001 Infinity 1", "monster 1001 -1 1", "monster 1001 32000 1", "monster 1001 1 32000", "monster 1001 nope 1",
                "monster 0 1 1", "monster -1 1 1", "monster 1.5 1 1", "monster 2147483648 1 1" })
            {
                Assert.IsType<ErrorRt>((await Exchange(server, one, new Gm { Command = command })).Last());
                var frame = Assert.IsType<ResyncWorldRt>((await Exchange(server, one, new ResyncWorld())).Last()).Frame;
                Assert.Empty(frame.Snapshot.Map.Chunks.SelectMany(chunk => chunk.Objects));
            }
            Assert.False(File.Exists(Path.Combine(_root, "maps", "1", "manifest.json")));
            var reply = Assert.IsType<GmRt>(Assert.Single(await Exchange(server, one, new Gm { Command = "monster 1001 16080.25 16032.5" })));
            Assert.Contains("树妖", reply.Message); Assert.Contains("16080.25", reply.Message); Assert.Contains("16032.5", reply.Message);
            await Task.Delay(110); await server.Server!.AdvanceAsync();
            var observations = new[] { ReadAll(server, one), ReadAll(server, two) };
            var first = observations[0].OfType<WorldUpdateNotice>().SelectMany(notice => notice.Frame.Delta?.Map?.UpsertedObjects.AsEnumerable() ?? []).ToArray();
            monster = Assert.Single(first); Assert.Equal("1001", monster.Monster.MonsterTypeId);
            Assert.Equal(16080.25f, monster.X); Assert.Equal(16032.5f, monster.Y);
            Assert.Contains(observations[1].OfType<WorldUpdateNotice>(), notice => notice.Frame.Delta?.Map?.UpsertedObjects.Any(value => value.Equals(monster)) == true);
            await server.FlushAsync();
            await Exchange(server, one, new LeaveWorld());
            var returned = Assert.IsType<EnterWorldRt>((await Exchange(server, one, new EnterWorld { RoleId = roleId })).Last());
            Assert.Equal(monster, Assert.Single(returned.Map.Chunks.SelectMany(chunk => chunk.Objects)));
        }
        await using var restarted = MockServerService.CreateInProcess(env, config);
        var connection = restarted.OpenConnection();
        await Exchange(restarted, connection, new Login { Username = "demo1", Password = "123" });
        var restored = Assert.IsType<EnterWorldRt>((await Exchange(restarted, connection, new EnterWorld { RoleId = roleId })).Last());
        Assert.Equal(monster, Assert.Single(restored.Map.Chunks.SelectMany(chunk => chunk.Objects)));
    }

    /// <summary>对应basic-monster-combat：即时起手对双方可见，移动取消不产生伤害，其他玩家可独立命中。</summary>
    [Fact]
    public async Task CombatEventsReachBothObserversAndMovementCancelsWindup()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory:_root); using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env,config);
        var one=server.OpenConnection();var two=server.OpenConnection();
        var entry=await Enter(server,one,"demo1");await Enter(server,two,"demo2");
        var position=entry.State.Position;
        await Exchange(server,one,new Gm { Command=FormattableString.Invariant($"monster 1001 {position.X+20:R} {position.Y:R}") });
        var map=Assert.IsType<ResyncWorldRt>((await Exchange(server,one,new ResyncWorld())).Last()).Frame.Snapshot.Map;
        var target=Assert.Single(map.Chunks.SelectMany(chunk=>chunk.Objects)); ReadAll(server,two);
        var first=await Exchange(server,one,new Attack { AttackId=1,TargetId=target.ObjectId });
        Assert.True(Assert.IsType<AttackRt>(first.Last()).Accepted);
        Assert.Contains(first,value=>value is AttackStartedNotice);
        Assert.Contains(ReadAll(server,two),value=>value is AttackStartedNotice);
        await Exchange(server,one,new Move { ActionId=2,Target=new() { X=position.X,Y=position.Y } });
        Assert.Contains(ReadAll(server,two),value=>value is AttackCancelledNotice);
        await Task.Delay(380);await server.Server!.AdvanceAsync();
        Assert.DoesNotContain(ReadAll(server,one),value=>value is AttackHitNotice);
        var unchanged=Assert.IsType<ResyncWorldRt>((await Exchange(server,one,new ResyncWorld())).Last()).Frame.Snapshot.Map;
        Assert.Equal(100,Assert.Single(unchanged.Chunks.SelectMany(chunk=>chunk.Objects)).Monster.Health);
        var other=await Exchange(server,two,new Attack { AttackId=1,TargetId=target.ObjectId });
        Assert.True(Assert.IsType<AttackRt>(other.Last()).Accepted);ReadAll(server,one);
        await Task.Delay(380);await server.Server.AdvanceAsync();
        Assert.Single(ReadAll(server,one).OfType<AttackHitNotice>());
        Assert.Single(ReadAll(server,two).OfType<AttackHitNotice>());
    }

    /// <summary>真实账号请求创建并进入一个角色。</summary>
    private async Task<EnterWorldRt> Enter(MockServerService server, DataConnectionId connection, string username)
    {
        await Exchange(server, connection, new Login { Username = username, Password = "123" });
        var role = Assert.Single(Assert.IsType<CreateRoleRt>((await Exchange(server, connection,
            new CreateRole { Name = username, HeadId = 1, Gender = RoleGenderPb.Male })).Last()).Roles);
        return Assert.IsType<EnterWorldRt>((await Exchange(server, connection, new EnterWorld { RoleId = role.RoleId })).Last());
    }

    /// <summary>保留推送并等待关联回复，所有业务经过正式字节协议。</summary>
    private async Task<List<IMessage>> Exchange(MockServerService server, DataConnectionId connection, IMessage request)
    {
        ReadAll(server, connection);
        var number = ++_request;
        await server.ReceiveAsync(connection, Codec.Encode(request, server.Session(connection).Generation, number, true)).WaitAsync(TimeSpan.FromSeconds(5));
        var result = new List<IMessage>();
        while (server.TryReadOutgoing(connection, out var packet))
        {
            var envelope = Codec.ReadEnvelope(packet); result.Add(Codec.ReadPayload(envelope, false));
            if (envelope.RequestId == number) return result;
        }
        throw new InvalidOperationException("怪物请求没有关联回复");
    }

    /// <summary>排空指定连接的已有输出。</summary>
    private static List<IMessage> ReadAll(MockServerService server, DataConnectionId connection)
    {
        var result = new List<IMessage>();
        while (server.TryReadOutgoing(connection, out var packet)) result.Add(Codec.ReadPayload(Codec.ReadEnvelope(packet), false));
        return result;
    }

    /// <summary>回收本例创建的文件。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
