using System.Collections.Concurrent;
using Google.Protobuf;
using Rpg.Protocol;
using Rpg.Server.Connections.Data;
using Rpg.Server.Hosting;
using Rpg.Server.Modules.Common.Data;
using Rpg.Server.Modules.Login.Data;
using Rpg.Server.Modules.Login;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.World;
using Rpg.Server.Modules;
using Rpg.Server.Protocol;
using Rpg.Server.Runtime;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-network、server-account 的真实字节与身份边界。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerNetworkTests : ServerModuleTests
{
    /// <summary>验证 server-runtime：路由和邮箱直接传递原协议实例，回复类型错误不影响后续回合。</summary>
    [Fact]
    public async Task ProtocolRequestAndReplyPassDirectlyThroughRouterAndActor()
    {
        var seen = new ConcurrentQueue<(string Account, Login Request)>();
        var reply = new LoginRt { Roles = { new RoleSummaryPb { RoleId = 101, Name = "直接回复" } } };
        await using var modules = new DoubleServerModules(Config, new DoubleServerStorage(), new DoubleServerStorage(), new DoubleServerStorage(), _ =>
            new ActorHandlers<LoginContext>().Register<Login, LoginRt>(new DoubleProtocolHandler(seen, reply)));
        var router = new Rpg.Server.Routing.RequestRouter(modules);
        var request = new Login { Username = "  ALICE ", Password = "原始密码" };
        var result = await router.RouteAsync(new DataSessionSnapshot(new DataConnectionId(Guid.NewGuid()), 1, null), request);
        Assert.Same(reply, result.Message);
        Assert.Equal("alice", result.BindUsername);
        Assert.True(seen.TryDequeue(out var observed));
        Assert.Equal("alice", observed.Account);
        Assert.Same(request, observed.Request);
        Assert.Equal("  ALICE ", observed.Request.Username);
        Assert.Equal("原始密码", observed.Request.Password);

        var actor = modules.Login("alice");
        await Assert.ThrowsAsync<InvalidOperationException>(() => actor.Ask<string>(new Login()));
        Assert.Same(reply, await actor.Ask<LoginRt>(new Login()));
    }

    /// <summary>相同规范账号共用 Actor，不同连接仍隔离输出和登录绑定。</summary>
    [Fact]
    public async Task ConnectionsShareAccountShardButKeepSeparateSessionsAndPackets()
    {
        var created = new ConcurrentDictionary<string, int>();
        await using var server = CreateServer(created);
        var one = server.OpenConnection();
        var two = server.OpenConnection();
        var three = server.OpenConnection();
        var bytes = MessageCodec.Encode(new Login { Username = "  ALICE ", Password = "secret" }, 1, 42, true);
        var first = server.ReceiveAsync(one, bytes);
        Array.Fill(bytes, (byte)0); // 验证接纳时已复制，不持有调用方数组。
        await Task.WhenAll(first, server.ReceiveAsync(two, LoginPacket("alice", 42)), server.ReceiveAsync(three, LoginPacket("bob", 42)));
        Assert.Equal(2, created.Count);
        Assert.All(created.Values, count => Assert.Equal(1, count));
        Assert.Equal("alice", server.Session(one).Username);
        Assert.Equal("alice", server.Session(two).Username);
        Assert.Equal("bob", server.Session(three).Username);
        Assert.Equal("alice", await server.Router.AskAccountAsync(server.Session(one), new DataReadAccount()));
        foreach (var id in new[] { one, two, three })
        {
            Assert.True(server.TryReadOutgoing(id, out var packet));
            var envelope = MessageCodec.ReadEnvelope(packet);
            Assert.Equal(42UL, envelope.RequestId);
            Assert.Equal(1UL, envelope.Generation);
            Assert.Empty(Assert.IsType<LoginRt>(MessageCodec.ReadPayload(envelope, false)).Roles);
            Assert.False(server.TryReadOutgoing(id, out _));
        }
        Assert.Equal(2UL, server.ResetSession(one));
        Assert.Equal("alice", server.Session(one).Username);
        Assert.Equal(1UL, server.Session(two).Generation);
    }

    /// <summary>迟到登录不能绑定新代次，其他账号与连接无需等待阻塞的账号分片。</summary>
    [Fact]
    public async Task ResetAndDisconnectInvalidateLateLoginWithoutBlockingOtherAccounts()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateServer(handler: new DoubleLoginHandler(entered, release.Task));
        var one = server.OpenConnection();
        var two = server.OpenConnection();
        var old = server.ReceiveAsync(one, LoginPacket("blocked", 1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        server.ResetSession(one);
        await server.ReceiveAsync(two, LoginPacket("free", 1)).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("free", server.Session(two).Username);
        Assert.False(old.IsCompleted);
        release.SetResult();
        await old;
        Assert.Null(server.Session(one).Username);
        Assert.False(server.TryReadOutgoing(one, out _));
        await server.ReceiveAsync(one, LoginPacket("stale", 2));
        Assert.Null(server.Session(one).Username);
        await server.ReceiveAsync(one, LoginPacket("new", 3, 2));
        Assert.Equal("new", server.Session(one).Username);
        server.ResetSession(one);
        Assert.False(server.TryReadOutgoing(one, out _));
        await server.CloseConnectionAsync(one);
        Assert.Throws<InvalidOperationException>(() => server.Session(one));
        Assert.NotEqual(one, server.OpenConnection());
    }

    /// <summary>断开立即使身份失效；宿主停止仍等待该连接已接纳回合。</summary>
    [Fact]
    public async Task ClosingConnectionAndHostWaitForAcceptedWork()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateServer(handler: new DoubleLoginHandler(entered, release.Task));
        var id = server.OpenConnection();
        var work = server.ReceiveAsync(id, LoginPacket("blocked", 1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var close = server.CloseConnectionAsync(id);
        var stop = server.StopAsync();
        Assert.False(close.IsCompleted);
        Assert.False(stop.IsCompleted);
        Assert.Throws<ObjectDisposedException>(() => server.OpenConnection());
        release.SetResult();
        await Task.WhenAll(work, close, stop).WaitAsync(TimeSpan.FromSeconds(5));
    }

    /// <summary>正常停服先完成原请求及其输出，再停止账号 Actor。</summary>
    [Fact]
    public async Task HostShutdownPreservesAcceptedResponseAndSummaryProjection()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new ServerRuntime(new DoubleServerModules(Config, new DoubleServerStorage(), new DoubleServerStorage(), new DoubleServerStorage(), _ => new ActorHandlers<LoginContext>()
            .Register<Login, LoginRt>(new DoubleSummaryHandler(entered, release.Task))));
        var id = server.OpenConnection();
        var work = server.ReceiveAsync(id, LoginPacket("alice", 11));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stopping = server.StopAsync();
        Assert.False(stopping.IsCompleted);
        release.SetResult();
        await Task.WhenAll(work, stopping).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(server.TryReadOutgoing(id, out var bytes));
        var response = Assert.IsType<LoginRt>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(bytes), false));
        var role = Assert.Single(response.Roles);
        Assert.Equal(101, role.RoleId);
        Assert.Equal("账号角色", role.Name);
        Assert.Equal(7, role.Level);
        Assert.Equal(RoleGenderPb.Female, role.Gender);
        Assert.Equal(9, role.HeadId);
        Assert.Equal(new[] { 31, 42 }, role.EquippedItems);
    }

    /// <summary>错误方向、未知类型及损坏载荷返回可关联 ErrorRt，未解析信封不分发。</summary>
    [Fact]
    public async Task ProtocolErrorsAreCorrelatedAndDoNotPoisonConnection()
    {
        await using var server = CreateServer();
        var id = server.OpenConnection();
        foreach (var (type, payload, code) in new[]
        {
            ("rpg.Unknown", ByteString.Empty, ErrorCodePb.UnknownMessage),
            (LoginRt.Descriptor.FullName, ByteString.Empty, ErrorCodePb.WrongDirection),
            (Login.Descriptor.FullName, ByteString.CopyFrom(new byte[] { 0xff }), ErrorCodePb.InvalidRequest)
        })
        {
            await server.ReceiveAsync(id, new EnvelopePb { Generation = 1, RequestId = 7, MessageType = type, Payload = payload }.ToByteArray());
            Assert.True(server.TryReadOutgoing(id, out var packet));
            var envelope = MessageCodec.ReadEnvelope(packet);
            Assert.Equal(7UL, envelope.RequestId);
            Assert.Equal(code, Assert.IsType<ErrorRt>(MessageCodec.ReadPayload(envelope, false)).Code);
            Assert.Null(server.Session(id).Username);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => server.ReceiveAsync(id, new byte[] { 0xff }));
        await Assert.ThrowsAsync<ServerRequestException>(() => server.ReceiveAsync(id,
            new EnvelopePb { Generation = 1, RequestId = 0, MessageType = Login.Descriptor.FullName }.ToByteArray()));
        await server.ReceiveAsync(id, LoginPacket("alice", 8));
        Assert.Equal("alice", server.Session(id).Username);
    }

    /// <summary>登录失败不会保留旧账号，意外处理器错误可诊断且不阻断后续请求。</summary>
    [Fact]
    public async Task FailedLoginClearsBindingAndUnexpectedFailureIsObservable()
    {
        await using var server = CreateServer();
        var errors = new List<Exception>();
        server.Error += _ => throw new InvalidOperationException("观察者失败");
        server.Error += errors.Add;
        var id = server.OpenConnection();
        await server.ReceiveAsync(id, LoginPacket("alice", 1));
        Assert.True(server.TryReadOutgoing(id, out _));
        foreach (var username in new[] { "denied", "crash" })
        {
            await server.ReceiveAsync(id, LoginPacket(username, 2));
            Assert.Null(server.Session(id).Username);
            Assert.True(server.TryReadOutgoing(id, out var packet));
            var failure = Assert.IsType<ErrorRt>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
            Assert.Equal(username == "denied" ? ErrorCodePb.NotAuthenticated : ErrorCodePb.InternalError, failure.Code);
            await Assert.ThrowsAsync<ServerRequestException>(() => server.Router.AskAccountAsync(server.Session(id), new DataReadAccount()));
        }
        Assert.Single(errors);
        await server.ReceiveAsync(id, LoginPacket("bob", 3));
        Assert.Equal("bob", server.Session(id).Username);
    }

    /// <summary>账号键与文件名使用同一身份，非法 Windows 名称在框架入口拒绝。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("../account")]
    [InlineData("name.")]
    [InlineData("a:b")]
    [InlineData("CON")]
    [InlineData("LPT1.extra")]
    [InlineData("com¹")]
    [InlineData("con .extra")]
    [InlineData("a\nb")]
    public void AccountStorageContractRejectsUnsafeKeys(string username)
    {
        Assert.Throws<ArgumentException>(() => UsernameKey.Normalize(username));
        Assert.Equal("alice", UsernameKey.Normalize("  ALICE "));
        Assert.Equal("alice.role_summary.json", UsernameKey.SummaryFileName("  ALICE "));
        Assert.Throws<ArgumentException>(() => UsernameKey.SummaryFileName(username));
        Assert.Throws<ArgumentException>(() => UsernameKey.Normalize(new string('a', 238)));
    }

    /// <summary>角色、地图按身份独立运行；未知消息不污染冻结的处理器注册。</summary>
    [Fact]
    public async Task RoleAndWorldModulesKeepSeparateIdentityAndFrozenHandlers()
    {
        var handlers = new ActorHandlers<RoleContext>().Register<DataRoleIdentity, long>(new DoubleRoleHandler());
        await using var modules = new DoubleServerModules(Config, new DoubleServerStorage(), new DoubleServerStorage(), new DoubleServerStorage(), _ => LoginHandlers(new DoubleLoginHandler()),
            _ => handlers, _ => new ActorHandlers<WorldContext>().Register<DataMapIdentity, int>(new DoubleWorldHandler()));
        var role = modules.Role(10);
        var world = modules.World(1);
        Assert.Equal(ActorState.INIT, role.State);
        Assert.Equal(10, await role.Ask(new DataRoleIdentity()));
        Assert.Equal(1, await world.Ask(new DataMapIdentity()));
        Assert.Same(role, modules.Role(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => modules.World(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => modules.Role(-1));
        Assert.Throws<InvalidOperationException>(() => handlers.Register<DataRoleIdentity, long>(new DoubleRoleHandler()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => role.Ask(new DataMapIdentity()));
        Assert.Equal(10, await role.Ask(new DataRoleIdentity()));
    }

    /// <summary>使用本例配置与内存存储装配框架验收实例。</summary>
    private ServerRuntime CreateServer(ConcurrentDictionary<string, int>? created = null, DoubleLoginHandler? handler = null) =>
        new(new DoubleServerModules(Config, new DoubleServerStorage(), new DoubleServerStorage(), new DoubleServerStorage(), username =>
        {
            created?.AddOrUpdate(username, 1, (_, count) => count + 1);
            return LoginHandlers(handler ?? new DoubleLoginHandler());
        }));
    /// <summary>显式注册测试登录及不携带 username 的账号请求。</summary>
    private static ActorHandlers<LoginContext> LoginHandlers(DoubleLoginHandler handler) => new ActorHandlers<LoginContext>()
        .Register<Login, LoginRt>(handler).Register<DataReadAccount, string>(new DoubleAccountHandler());
    /// <summary>序列化真实 登录包。</summary>
    private static byte[] LoginPacket(string username, ulong requestId, ulong generation = 1) =>
        MessageCodec.Encode(new Login { Username = username, Password = "secret" }, generation, requestId, true);

    /// <summary>测试处理器自行组装协议回复，路由不再承担摘要投影。</summary>
    internal static LoginRt LoginResponse(LoginContext context)
    {
        var response = new LoginRt();
        foreach (var role in context.Cache.Summaries.Value)
        {
            var summary = new RoleSummaryPb
            { RoleId = role.RoleId, Name = role.Name, Level = role.Level, Gender = (RoleGenderPb)role.Gender, HeadId = role.HeadId };
            if (!role.EquippedItems.IsDefault) summary.EquippedItems.AddRange(role.EquippedItems);
            response.Roles.Add(summary);
        }
        return response;
    }

    /// <summary>观察原协议请求和所属账号，直接返回预置协议回复以验证传递契约。</summary>
    private sealed class DoubleProtocolHandler(ConcurrentQueue<(string Account, Login Request)> seen, LoginRt reply)
        : IActorHandler<LoginContext, Login, LoginRt>
    {
        /// <summary>记录实际收到的实例，测试完成移交后不再修改请求与回复。</summary>
        public ValueTask<LoginRt> HandleAsync(LoginContext context, Login request)
        {
            seen.Enqueue((context.Cache.Username, request));
            return ValueTask.FromResult(reply);
        }
    }

    /// <summary>验证后续账号操作只使用服务端会话，不重复携带 username。</summary>
    private sealed record DataReadAccount : IActorRequest<string>;
    /// <summary>测试只读账号身份的处理器契约。</summary>
    private sealed class DoubleAccountHandler : IActorHandler<LoginContext, DataReadAccount, string>
    {
        /// <summary>从所属上下文返回不可变账号键。</summary>
        public ValueTask<string> HandleAsync(LoginContext context, DataReadAccount request) => ValueTask.FromResult(context.Cache.Username);
    }
    /// <summary>无认证业务的内存替身，仅服务成功、失败与迟到结果契约。</summary>
    internal sealed class DoubleLoginHandler(TaskCompletionSource? entered = null, Task? release = null)
        : IActorHandler<LoginContext, Login, LoginRt>
    {
        /// <summary>明确模拟阻塞和失败，不读取密码或文件。</summary>
        public async ValueTask<LoginRt> HandleAsync(LoginContext context, Login request)
        {
            if (context.Cache.Username == "blocked") { entered?.TrySetResult(); if (release is not null) await release.ConfigureAwait(false); }
            if (context.Cache.Username == "denied") throw new ServerRequestException(ErrorCodePb.NotAuthenticated, "测试拒绝登录");
            if (context.Cache.Username == "crash") throw new InvalidOperationException("处理器意外失败");
            return ServerNetworkTests.LoginResponse(context);
        }
    }
    /// <summary>验证账号摘要内部数据到协议的投影与正常关闭排空，不实现认证。</summary>
    private sealed class DoubleSummaryHandler(TaskCompletionSource entered, Task release)
        : IActorHandler<LoginContext, Login, LoginRt>
    {
        /// <summary>等待回合屏障后接纳不可变摘要，并返回本账号快照。</summary>
        public async ValueTask<LoginRt> HandleAsync(LoginContext context, Login request)
        {
            entered.TrySetResult();
            await release.ConfigureAwait(false);
            context.Cache.Commit(new(
                [new DataRoleSummary(101, "账号角色", 7, Rpg.Server.Modules.Common.Data.RoleGender.Female, 9, [31, 42])]));
            return ServerNetworkTests.LoginResponse(context);
        }
    }
    /// <summary>只用于角色身份契约的不可变请求。</summary>
    private sealed record DataRoleIdentity : IActorRequest<long>;
    /// <summary>只用于地图身份契约的不可变请求。</summary>
    private sealed record DataMapIdentity : IActorRequest<int>;
    /// <summary>角色上下文契约替身。</summary>
    private sealed class DoubleRoleHandler : IActorHandler<RoleContext, DataRoleIdentity, long>
    {
        /// <summary>返回所属角色身份。</summary>
        public ValueTask<long> HandleAsync(RoleContext context, DataRoleIdentity request) => ValueTask.FromResult(context.Cache.RoleId);
    }
    /// <summary>地图上下文契约替身。</summary>
    private sealed class DoubleWorldHandler : IActorHandler<WorldContext, DataMapIdentity, int>
    {
        /// <summary>返回所属地图身份。</summary>
        public ValueTask<int> HandleAsync(WorldContext context, DataMapIdentity request) => ValueTask.FromResult(context.Cache.MapId);
    }
}
