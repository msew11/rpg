using Rpg.Services;
using Rpg.Session.Data;
using Google.Protobuf;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Session;

namespace ClientTests;

/// <summary>验证 openspec/specs/client-network 的字节、方向、队列与代次契约。</summary>
public sealed class NetworkFoundationTests : EventBusServiceTestScope
{
    /// <summary>验证 client-session：登录摘要身份、名称、等级和性别不合法时拒绝转换。</summary>
    [Theory]
    [InlineData(0, "晨星", 1, RoleGenderPb.Male)]
    [InlineData(1001, " ", 1, RoleGenderPb.Male)]
    [InlineData(1001, "晨星", 0, RoleGenderPb.Male)]
    [InlineData(1001, "晨星", 1, RoleGenderPb.Unspecified)]
    [InlineData(1001, "晨星", 1, (RoleGenderPb)99)]
    public void InvalidLoginSummaryIsRejected(long roleId, string name, int level, RoleGenderPb gender)
    {
        var summary = new RoleSummaryPb { RoleId = roleId, Name = name, Level = level, Gender = gender };
        Assert.Throws<InvalidDataException>(() => RoleSummaryPbData.FromPb(summary));
    }

    /// <summary>所有首批协议都能独立序列化并保留业务数据。</summary>
    [Fact]
    public void ProtocolsRoundTripAsIndependentObjects()
    {
        var state = new RoleWorldStatePb {
            Profile = new RoleWorldProfilePb { RoleId = 1001, Name = "晨星", Gender = RoleGenderPb.Male,
                Revision = 9, HeadId = 74 },
            Position = new RoleWorldPositionPb { RoleId = 1001, MapId = 1, X = 123.5f, Y = 456,
                Revision = 9, AcknowledgedAction = 3 } };
        var summary = new RoleSummaryPb { RoleId = 1001, Name = "晨星", Level = 1, Gender = RoleGenderPb.Male };
        Assert.Equal(RoleWorldPositionPb.Descriptor, MoveRt.Descriptor.FindFieldByName("position").MessageType);
        Assert.Null(MoveRt.Descriptor.FindFieldByName("state"));
        IMessage[] messages = [new Login { Username = "demo", Password = "demo" },
            new SelectRole { RoleId = 1001 }, new Move { RoleId = 1001, ActionId = 3, X = 9, Y = 8 },
            new CreateRole { Name = "创建角色", Gender = RoleGenderPb.Female, HeadId = 74 },
            new DeleteRole { RoleId = 1001 },
            new CreateRoleRt { Success = true, Roles = { summary } },
            new DeleteRoleRt { Success = true },
            new LoginRt { Success = true, Roles = { summary } },
            new SelectRoleRt { Success = true, RoleId = 1001, State = state },
            new MoveRt { Success = true, RoleId = 1001, ActionId = 3, Position = state.Position },
            new RoleStateNotice { State = state },
            new RoleProfileNotice { Profile = state.Profile },
            new RolePositionNotice { Position = state.Position }];
        for (var index = 0; index < messages.Length; index++)
        {
            var request = messages[index] is Login or SelectRole or CreateRole or DeleteRole or Move;
            var envelope = MessageCodec.ReadEnvelope(MessageCodec.Encode(messages[index], 7, request));
            var decoded = MessageCodec.ReadPayload(envelope, request);
            Assert.Equal(7, envelope.Generation);
            Assert.Equal(messages[index], decoded);
            Assert.NotSame(messages[index], decoded);
        }
    }

    /// <summary>未知类型、错误方向和损坏字节均阻止业务分发。</summary>
    [Fact]
    public void InvalidPacketsReportErrorsAndDoNotDispatch()
    {
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, EventBus, new Rpg.Services.LogService(Console.WriteLine));
        var errors = new List<Exception>();
        var delivered = 0;
        net.Error += errors.Add;
        using var subscription = EventBus.Subscribe<LoginRt>(_ => delivered++);
        transport.Inject([255]);
        transport.Inject(new EnvelopePb { Generation = 1, MessageType = "unknown" }.ToByteArray());
        transport.Inject(MessageCodec.Encode(new Login(), 1, true));
        transport.Inject(new EnvelopePb { Generation = 1, MessageType = LoginRt.Descriptor.FullName,
            Payload = ByteString.CopyFrom([255]) }.ToByteArray());
        net.Tick(0);
        Assert.Equal(4, errors.Count);
        Assert.Equal(0, delivered);
        Assert.Throws<InvalidDataException>(() => net.Send(new LoginRt()));
    }

    /// <summary>Send 冻结请求，收到的响应只在 Tick 分发，回调中的新请求留到下一帧。</summary>
    [Fact]
    public void SendsAndReceivesAreQueuedAndFreezeMutableRequests()
    {
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, EventBus, new Rpg.Services.LogService(Console.WriteLine));
        var callbacks = 0;
        using var subscription = EventBus.Subscribe<LoginRt>(_ => { callbacks++; net.Send(new SelectRole { RoleId = 1001 }); });
        var request = new Login { Username = "demo", Password = "demo" };
        net.Send(request);
        request.Username = "changed";
        Assert.Empty(transport.Sent);
        net.Tick(0);
        var decoded = Assert.IsType<Login>(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(transport.Sent[0]), true));
        Assert.Equal("demo", decoded.Username);
        transport.Inject(MessageCodec.Encode(new LoginRt { Success = true }, 1, false));
        Assert.Equal(0, callbacks);
        net.Tick(0);
        Assert.Equal(1, callbacks);
        Assert.Single(transport.Sent);
        net.Tick(0);
        Assert.Equal(2, transport.Sent.Count);
    }

    /// <summary>重置会话丢弃未发送请求和已经在途的旧代次响应。</summary>
    [Fact]
    public void SessionResetDiscardsOldQueuedAndInFlightMessages()
    {
        using var transport = new DoubleNetTransport();
        using var net = new NetService(transport, EventBus, new Rpg.Services.LogService(Console.WriteLine));
        var calls = 0;
        using var subscription = EventBus.Subscribe<LoginRt>(_ => calls++);
        net.Send(new Login());
        net.ResetSession();
        transport.Inject(MessageCodec.Encode(new LoginRt(), 1, false));
        net.Tick(0);
        Assert.Empty(transport.Sent);
        Assert.Equal(0, calls);
        transport.Inject(MessageCodec.Encode(new LoginRt(), 2, false));
        net.Tick(0);
        Assert.Equal(1, calls);
    }
}

/// <summary>验证 openspec/specs/session-events 的优先级、生命周期、重入与异常场景。</summary>
public sealed class SessionEventTests : EventBusServiceTestScope
{
    /// <summary>先注册的 UI 也能读到所有缓存监听处理后的值。</summary>
    [Fact]
    public void AllCachesRunBeforeEarlierRegisteredUi()
    {
        var eventBus = EventBus;
        var order = new List<string>();
        var value = 0;
        using var ui = eventBus.Subscribe<int>(_ => { Assert.Equal(2, value); order.Add("ui"); });
        using var first = eventBus.Subscribe<int>(_ => { value++; order.Add("cache1"); }, EventPhase.Cache);
        using var second = eventBus.Subscribe<int>(_ => { value++; order.Add("cache2"); }, EventPhase.Cache);
        eventBus.Publish(1);
        Assert.Equal(new[] { "cache1", "cache2", "ui" }, order);
    }

    /// <summary>快照不包含新监听，并会跳过分发中取消的旧监听。</summary>
    [Fact]
    public void RegistrationAndDisposalDuringDispatchRespectSnapshot()
    {
        var eventBus = EventBus;
        IDisposable? removed = null;
        IDisposable? added = null;
        var order = new List<string>();
        using var first = eventBus.Subscribe<int>(_ =>
        {
            order.Add("first");
            removed!.Dispose();
            added ??= eventBus.Subscribe<int>(_ => order.Add("added"));
        });
        removed = eventBus.Subscribe<int>(_ => order.Add("removed"));
        eventBus.Publish(1);
        Assert.Equal(new[] { "first" }, order);
        eventBus.Publish(2);
        Assert.Equal(new[] { "first", "first", "added" }, order);
        added?.Dispose();
        added?.Dispose();
    }

    /// <summary>缓存回调的嵌套发布不会抢在当前 UI 前执行。</summary>
    [Fact]
    public void ReentrantMessagesWaitForCurrentNormalPhase()
    {
        var eventBus = EventBus;
        var order = new List<string>();
        using var cache = eventBus.Subscribe<int>(value =>
        {
            order.Add($"cache{value}");
            if (value == 1) eventBus.Publish(2);
        }, EventPhase.Cache);
        using var ui = eventBus.Subscribe<int>(value => order.Add($"ui{value}"));
        eventBus.Publish(1);
        Assert.Equal(new[] { "cache1", "ui1", "cache2", "ui2" }, order);
    }

    /// <summary>缓存异常跳过 UI，普通异常不阻断其他监听或后续消息。</summary>
    [Fact]
    public void ListenerFailuresAreIsolatedByPhase()
    {
        var eventBus = EventBus;
        var errors = 0;
        var calls = 0;
        eventBus.Error += _ => errors++;
        eventBus.Error += _ => throw new InvalidOperationException("错误观察者异常");
        using var cache = eventBus.Subscribe<int>(value => { if (value == 1) throw new InvalidOperationException("缓存失败"); }, EventPhase.Cache);
        using var badUi = eventBus.Subscribe<int>(_ => throw new InvalidOperationException("UI 失败"));
        using var goodUi = eventBus.Subscribe<int>(_ => calls++);
        eventBus.Publish(1);
        Assert.Equal(0, calls);
        eventBus.Publish(2);
        Assert.Equal(1, calls);
        Assert.Equal(2, errors);
    }

    /// <summary>会话重置中止旧消息剩余监听并清理嵌套消息。</summary>
    [Fact]
    public void ResetDuringDispatchInvalidatesCurrentSnapshotAndQueue()
    {
        var eventBus = EventBus;
        var calls = 0;
        using var cache = eventBus.Subscribe<int>(_ => { eventBus.Publish("old"); eventBus.ResetPending(); }, EventPhase.Cache);
        using var ui = eventBus.Subscribe<int>(_ => calls++);
        using var nested = eventBus.Subscribe<string>(_ => calls++);
        eventBus.Publish(1);
        Assert.Equal(0, calls);
    }
}

/// <summary>服务于 client-network 字节边界契约的可控传输替身。</summary>
internal sealed class DoubleNetTransport : INetTransport
{
    /// <summary>捕获发送方冻结的字节。</summary>
    public List<byte[]> Sent { get; } = [];
    /// <summary>模拟传输发送失败，服务于通信日志的失败隔离契约。</summary>
    public bool FailSend { get; init; }
    /// <summary>向网络服务提交测试字节。</summary>
    public event Action<byte[]>? Received;
    /// <summary>记录发送数据，不执行任何业务逻辑。</summary>
    public void Send(byte[] packet)
    {
        if (FailSend) throw new IOException("模拟发送失败");
        Sent.Add((byte[])packet.Clone());
    }
    /// <summary>此替身不需要时间推进。</summary>
    public void Tick(double delta) { }
    /// <summary>保留已发送记录供断言，代次由信封验证。</summary>
    public void ResetSession(long generation) { }
    /// <summary>模拟在途服务器信封到达。</summary>
    public void Inject(byte[] packet) => Received?.Invoke(packet);
    /// <summary>释放接收订阅。</summary>
    public void Dispose() => Received = null;
}
