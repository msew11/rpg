using Google.Protobuf;
using Rpg.Config;
using Rpg.Protocol;
using Rpg.Server.Connections.Data;
using Rpg.Server.Hosting;
using Rpg.Server.Modules.Role.Storage;
using Codec = Rpg.Server.Protocol.MessageCodec;

namespace ClientTests;

/// <summary>对应 openspec/specs/gm-movement：成功前落盘、失败重试、隔离与重启恢复。</summary>
[Trait("Runtime", "Godot")]
public sealed class GmMovementTests : IDisposable
{
    /// <summary>本例独立服务器目录。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-gm-speed-" + Guid.NewGuid());
    /// <summary>关联请求编号。</summary>
    private ulong _request;

    /// <summary>禁用周期保存仍立即落地，off 保留最近值，其他角色不受影响。</summary>
    [Fact]
    public async Task SpeedPersistsBeforeReplyAndRestoresAcrossRestart()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0; long roleId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var one = server.OpenConnection(); var first = await Enter(server, one, "demo1"); roleId = first.State.RoleId;
            var two = server.OpenConnection(); var second = await Enter(server, two, "demo2");
            Assert.False(first.Role.GmMovementSpeed.Enabled); Assert.Equal(180, first.State.Position.Speed);
            var reply = Assert.IsType<GmRt>(await Exchange(server, one, new Gm { Command = "movespeed 12345.67891" }));
            Assert.True(reply.MovementSpeed.Enabled); Assert.Equal(12345.67891, reply.MovementSpeed.Speed);
            var saved = await new RoleStorage(_root).LoadAsync(roleId);
            Assert.True(saved.Role.GmMovementSpeedOverrideEnabled); Assert.Equal(12345.67891, saved.Role.GmMovementSpeed);
            Assert.Contains("\"gmMovementSpeedOverrideEnabled\": true", File.ReadAllText(Path.Combine(_root, $"{roleId}.role.json")));
            var resync = Assert.IsType<ResyncWorldRt>(await Exchange(server, one, new ResyncWorld()));
            Assert.Equal(12345.67891, resync.Frame.Self.Position.Speed);
            var other = Assert.IsType<ResyncWorldRt>(await Exchange(server, two, new ResyncWorld()));
            Assert.Equal(second.State.Position.Speed, other.Frame.Self.Position.Speed);
            foreach (var invalid in new[] { "NaN", "Infinity", "-1", "bad" })
                Assert.IsType<ErrorRt>(await Exchange(server, one, new Gm { Command = "movespeed " + invalid }));
            Assert.IsType<GmRt>(await Exchange(server, one, new Gm { Command = "movespeed 0" }));
            Assert.Equal(0, Assert.IsType<ResyncWorldRt>(await Exchange(server, one, new ResyncWorld())).Frame.Self.Position.Speed);
            await Exchange(server, one, new Gm { Command = "movespeed 12345.67891" });
        }
        await using (var restarted = MockServerService.CreateInProcess(env, config))
        {
            var id = restarted.OpenConnection(); await Exchange(restarted, id, new Login { Username = "demo1", Password = "123" });
            var restored = Assert.IsType<EnterWorldRt>(await Exchange(restarted, id, new EnterWorld { RoleId = roleId }));
            Assert.True(restored.Role.GmMovementSpeed.Enabled); Assert.Equal(12345.67891, restored.State.Position.Speed);
            Assert.Equal(180, restored.Role.Attributes.Values[1]);
            await Exchange(restarted, id, new Gm { Command = "movespeed off" });
            var saved = await new RoleStorage(_root).LoadAsync(roleId);
            Assert.False(saved.Role.GmMovementSpeedOverrideEnabled); Assert.Equal(12345.67891, saved.Role.GmMovementSpeed);
            await Exchange(restarted, id, new LeaveWorld());
            var reentry = Assert.IsType<EnterWorldRt>(await Exchange(restarted, id, new EnterWorld { RoleId = roleId }));
            Assert.False(reentry.Role.GmMovementSpeed.Enabled); Assert.Equal(12345.67891, reentry.Role.GmMovementSpeed.Speed);
            Assert.Equal(180, reentry.State.Position.Speed);
        }
    }

    /// <summary>文件锁造成保存失败时只回复错误，解锁后同命令可以重试成功。</summary>
    [Fact]
    public async Task SaveFailureDoesNotReportSuccessAndCanRetry()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _root); using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config); var id = server.OpenConnection();
        var entry = await Enter(server, id, "demo1"); var path = Path.Combine(_root, $"{entry.State.RoleId}.role.json");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.IsType<ErrorRt>(await Exchange(server, id, new Gm { Command = "movespeed 300.125" }));
        var reply = Assert.IsType<GmRt>(await Exchange(server, id, new Gm { Command = "movespeed 300.125" }));
        Assert.Equal(300.125, reply.MovementSpeed.Speed);
        Assert.Equal(300.125, (await new RoleStorage(_root).LoadAsync(entry.State.RoleId)).Role.GmMovementSpeed);
    }

    /// <summary>走真实登录和角色选择链路。</summary>
    private async Task<EnterWorldRt> Enter(MockServerService server, DataConnectionId id, string username)
    {
        await Exchange(server, id, new Login { Username = username, Password = "123" });
        var role = Assert.Single(Assert.IsType<CreateRoleRt>(await Exchange(server, id, new CreateRole { Name = username, Gender = RoleGenderPb.Male, HeadId = 1 })).Roles);
        return Assert.IsType<EnterWorldRt>(await Exchange(server, id, new EnterWorld { RoleId = role.RoleId }));
    }
    /// <summary>忽略独立通知，只读取本次关联响应。</summary>
    private async Task<IMessage> Exchange(MockServerService server, DataConnectionId id, IMessage request)
    {
        while (server.TryReadOutgoing(id, out _)) { }
        var number = ++_request;
        await server.ReceiveAsync(id, Codec.Encode(request, server.Session(id).Generation, number, true)).WaitAsync(TimeSpan.FromSeconds(5));
        while (server.TryReadOutgoing(id, out var packet))
        { var envelope = Codec.ReadEnvelope(packet); if (envelope.RequestId == number) return Codec.ReadPayload(envelope, false); }
        throw new InvalidOperationException("请求未回复");
    }
    /// <summary>只清理本例服务器目录。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
