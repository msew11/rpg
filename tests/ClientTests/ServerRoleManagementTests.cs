using Google.Protobuf;
using Rpg.Config;
using Rpg.Protocol;
using Rpg.Server.Connections.Data;
using Rpg.Server.Hosting;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login.Storage;
using Rpg.Server.Protocol;

namespace ClientTests;

/// <summary>通过真实字节和文件验证 openspec/specs/server-account 的摘要创建、墓碑及演示认证。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerRoleManagementTests : IDisposable
{
    /// <summary>每例独占临时目录，避免触及玩家存档。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-roles-" + Guid.NewGuid());
    /// <summary>每条请求使用独立关联编号。</summary>
    private ulong _requestId;

    /// <summary>首次登录、创建、删空和重启只保存摘要，墓碑编号不复用。</summary>
    [Fact]
    public async Task SummaryOnlyLifecycleSurvivesRestartWithoutRoleTables()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        env.ServerFlushIntervalSeconds = 0;
        long deletedId;
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var connection = server.OpenConnection();
            Assert.Empty(Assert.IsType<LoginRt>(await Exchange(server, connection, Credentials(" DEMO1 "))).Roles);
            Assert.Equal("demo1", server.Session(connection).Username);
            var created = Assert.IsType<CreateRoleRt>(await Exchange(server, connection, Create(" 首个角色 ")));
            var role = Assert.Single(created.Roles);
            deletedId = role.RoleId;
            Assert.Equal(1003, deletedId);
            Assert.Equal("首个角色", role.Name);
            Assert.Equal(1, role.Level);
            Assert.Empty(role.EquippedItems);
            server.Tick(100);
            Assert.False(File.Exists(Path.Combine(_directory, "demo1.role_summary.json")));
            Assert.Empty(Assert.IsType<DeleteRoleRt>(await Exchange(server, connection, new DeleteRole { RoleId = deletedId })).Roles);
            Assert.Equal(ErrorCodePb.RoleNotFound, Assert.IsType<ErrorRt>(await Exchange(server, connection, new DeleteRole { RoleId = deletedId })).Code);
            await server.FlushAsync();
        }
        var repo = new RepoAccountRoleSummary(Path.GetFullPath(_directory));
        Assert.True(Assert.Single(await repo.LoadAsync("demo1")).IsDeleted);
        Assert.Equal(new[] { "demo1.role_summary.json", "role_id_sequence.json", "role_id_sequence.json.lock" },
            Directory.GetFiles(Path.GetFullPath(_directory)).Select(Path.GetFileName).Order());
        Assert.Empty(Directory.GetDirectories(Path.GetFullPath(_directory)));
        await using (var server = MockServerService.CreateInProcess(env, config))
        {
            var connection = server.OpenConnection();
            Assert.Empty(Assert.IsType<LoginRt>(await Exchange(server, connection, Credentials())).Roles);
            var created = Assert.IsType<CreateRoleRt>(await Exchange(server, connection, Create("首个角色", RoleGenderPb.Female, 74)));
            Assert.True(Assert.Single(created.Roles).RoleId > deletedId);
        }
        Assert.Equal(2, (await repo.LoadAsync("demo1")).Length);
    }

    /// <summary>所有校验失败不消耗编号；名称比较、外观边界与溢出均由业务入口拒绝。</summary>
    [Fact]
    public async Task InvalidRequestsDoNotConsumeIdsOrChangeVisibleList()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var id = server.OpenConnection();
        Assert.Equal(ErrorCodePb.NotAuthenticated, Assert.IsType<ErrorRt>(await Exchange(server, id, Create("未认证"))).Code);
        await Exchange(server, id, Credentials());
        foreach (var request in new[] { Create(" "), Create(new string('a', 17)), Create("a\nb"),
                     Create("无效性别", RoleGenderPb.Unspecified), Create("无效头型", head: 0), Create("无效头型", head: 75) })
            Assert.Equal(ErrorCodePb.InvalidRequest, Assert.IsType<ErrorRt>(await Exchange(server, id, request)).Code);
        var first = Assert.IsType<CreateRoleRt>(await Exchange(server, id, Create(" Alice ", head: 1)));
        Assert.Equal(1003, Assert.Single(first.Roles).RoleId);
        Assert.Equal(ErrorCodePb.RoleNameConflict, Assert.IsType<ErrorRt>(await Exchange(server, id, Create("alice"))).Code);
        Assert.Equal(ErrorCodePb.InvalidRequest, Assert.IsType<ErrorRt>(await Exchange(server, id, new DeleteRole())).Code);
        Assert.Equal(ErrorCodePb.RoleNotFound, Assert.IsType<ErrorRt>(await Exchange(server, id, new DeleteRole { RoleId = 99 })).Code);
        var next = Assert.IsType<CreateRoleRt>(await Exchange(server, id, Create("第二角色", RoleGenderPb.Female, 72)));
        Assert.Equal(new long[] { 1003, 1004 }, next.Roles.Select(role => role.RoleId));
        Assert.Equal("Alice", next.Roles[0].Name);
        Assert.Equal(RoleGenderPb.Female, next.Roles[1].Gender);
    }

    /// <summary>重新登录失败解除旧绑定，规范账号与原样密码遵循既有连接代次。</summary>
    [Theory]
    [InlineData("demo1", " demo")]
    [InlineData("other", "demo1")]
    public async Task BadCredentialsClearBindingAndCannotManageRoles(string username, string password)
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var id = server.OpenConnection();
        await Exchange(server, id, Credentials());
        Assert.Equal(ErrorCodePb.InvalidCredentials, Assert.IsType<ErrorRt>(await Exchange(server, id, new Login { Username = username, Password = password })).Code);
        Assert.Null(server.Session(id).Username);
        Assert.Equal(ErrorCodePb.NotAuthenticated, Assert.IsType<ErrorRt>(await Exchange(server, id, new DeleteRole { RoleId = 1003 })).Code);
    }

    /// <summary>两个连接竞争同账号同名角色时最多成功一次，编号保持连续。</summary>
    [Fact]
    public async Task ConcurrentConnectionsSerializeNameCheckAndCommit()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var one = server.OpenConnection();
        var two = server.OpenConnection();
        await Exchange(server, one, Credentials());
        await Exchange(server, two, Credentials());
        var replies = await Task.WhenAll(Exchange(server, one, Create("并发")), Exchange(server, two, Create("并发")));
        Assert.Single(replies.OfType<CreateRoleRt>());
        Assert.Equal(ErrorCodePb.RoleNameConflict, Assert.Single(replies.OfType<ErrorRt>()).Code);
        var result = Assert.IsType<CreateRoleRt>(await Exchange(server, one, Create("后续")));
        Assert.Equal(new long[] { 1003, 1004 }, result.Roles.Select(role => role.RoleId));
    }

    /// <summary>全服编号耗尽使创建失败，账号摘要仍为空且不会回绕。</summary>
    [Fact]
    public async Task ExhaustedSequenceRejectsCreationWithoutChangingSummary()
    {
        var root = Path.GetFullPath(_directory);
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "role_id_sequence.json"), long.MaxValue.ToString());
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var id = server.OpenConnection();
        Assert.Empty(Assert.IsType<LoginRt>(await Exchange(server, id, Credentials())).Roles);
        Assert.Equal(ErrorCodePb.InvalidRequest, Assert.IsType<ErrorRt>(await Exchange(server, id, Create("溢出"))).Code);
        Assert.Empty(Assert.IsType<LoginRt>(await Exchange(server, id, Credentials())).Roles);
        Assert.False(File.Exists(Path.Combine(root, "demo1.role_summary.json")));
    }

    /// <summary>真实 IO 故障保留摘要变更，修复后显式保存和周期保存都可恢复。</summary>
    [Fact]
    public async Task FailedSaveCanRetryAndPeriodicSavePersistsOnlySummary()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var id = server.OpenConnection();
        await Exchange(server, id, Credentials());
        await Exchange(server, id, Create("待保存"));
        var file = Path.Combine(_directory, "demo1.role_summary.json");
        Directory.CreateDirectory(file);
        await Assert.ThrowsAsync<AggregateException>(() => server.FlushAsync());
        Directory.Delete(file);
        await server.FlushAsync();
        await Exchange(server, id, new DeleteRole { RoleId = 1003 });
        env.ServerFlushIntervalSeconds = 5;
        server.Tick(5);
        await server.PeriodicFlush;
        Assert.True(Assert.Single(await new RepoAccountRoleSummary(Path.GetDirectoryName(file)!).LoadAsync("demo1")).IsDeleted);
        Assert.Single(Directory.GetFiles(Path.GetFullPath(_directory), "*.role_summary.json"));
    }

    /// <summary>三个账号并发创建来自同一编号序列，列表和角色身份保持隔离。</summary>
    [Fact]
    public async Task ConcurrentAccountsShareOneSequence()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var accounts = new[] { "admin", "demo1", "demo2" };
        var connections = accounts.Select(_ => server.OpenConnection()).ToArray();
        for (var i = 0; i < accounts.Length; i++) await Exchange(server, connections[i], Credentials(accounts[i]));
        var replies = await Task.WhenAll(connections.Select(id => Exchange(server, id, Create("同名角色"))));
        var ids = replies.Select(reply => Assert.Single(Assert.IsType<CreateRoleRt>(reply).Roles).RoleId).ToArray();
        Assert.Equal(new long[] { 1003, 1004, 1005 }, ids.Order());
        for (var i = 0; i < accounts.Length; i++)
            Assert.Equal(ids[i], Assert.Single(Assert.IsType<LoginRt>(await Exchange(server, connections[i], Credentials(accounts[i]))).Roles).RoleId);
    }

    /// <summary>编号预留写入故障不产生摘要，修复后同一连接可以重新创建。</summary>
    [Fact]
    public async Task ReservationFailureLeavesNoSummaryAndCanRetry()
    {
        using var env = ConfigTestFixture.CreateEnv(serverDirectory: _directory);
        using var config = new ConfigService(env);
        await using var server = MockServerService.CreateInProcess(env, config);
        var connection = server.OpenConnection();
        await Exchange(server, connection, Credentials());
        var sequence = Path.Combine(_directory, "role_id_sequence.json");
        Directory.CreateDirectory(Path.GetDirectoryName(sequence)!);
        await File.WriteAllTextAsync(sequence, "1002");
        using (var locked = new FileStream(sequence, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.IsType<ErrorRt>(await Exchange(server, connection, Create("重试角色")));
        Assert.Empty(Assert.IsType<LoginRt>(await Exchange(server, connection, Credentials())).Roles);
        var created = Assert.IsType<CreateRoleRt>(await Exchange(server, connection, Create("重试角色")));
        Assert.Equal(1003, Assert.Single(created.Roles).RoleId);
    }

    /// <summary>按真实信封请求并检查成功、失败均保留请求关联和代次。</summary>
    private async Task<IMessage> Exchange(MockServerService server, DataConnectionId id, IMessage request)
    {
        var requestId = ++_requestId;
        await server.ReceiveAsync(id, MessageCodec.Encode(request, 1, requestId, true)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(server.TryReadOutgoing(id, out var bytes));
        var envelope = MessageCodec.ReadEnvelope(bytes);
        Assert.Equal(requestId, envelope.RequestId);
        Assert.Equal(1UL, envelope.Generation);
        return MessageCodec.ReadPayload(envelope, false);
    }
    /// <summary>构造本批演示凭据。</summary>
    private static Login Credentials(string username = "demo1") => new() { Username = username, Password = "123" };
    /// <summary>构造角色外观与名字意图，不携带账号或玩法数据。</summary>
    private static CreateRole Create(string name, RoleGenderPb gender = RoleGenderPb.Male, int head = 9) => new() { Name = name, Gender = gender, HeadId = head };
    /// <summary>只清理本例独占临时目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
