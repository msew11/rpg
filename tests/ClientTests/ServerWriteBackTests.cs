using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler.Bag;
using Rpg.Tests;
using Rpg.Services;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-write-back 的延迟合并、禁用、重试、墓碑及关闭落地。</summary>
public sealed class ServerWriteBackTests : IDisposable
{
    /// <summary>本次验收独占的存档目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-write-back-" + Guid.NewGuid());

    /// <summary>多次修改只落最终领域；恢复原值与空检测都不产生文件写入。</summary>
    [Fact]
    public void CoalescesDomainsAndSkipsUnchangedFiles()
    {
        var disk = new RepoServerRole(_directory);
        var original = disk.Create("合并测试", ServerRoleGender.Male, 1);
        var writes = new List<string>();
        using var server = new MockServer(_directory, new BagRules(8, 5, []), (path, _) => writes.Add(path));
        Enter(server, original.Role.Value.RoleId);
        server.PublishRolePosition(20, original.Role.Value.Y);
        server.PublishRolePosition(30, original.Role.Value.Y);
        Assert.Equal(original, disk.Load(original.Role.Value.RoleId));
        Assert.Empty(writes);
        server.Flush();
        Assert.Equal(disk.GetRolePath(original.Role.Value.RoleId), Assert.Single(writes));
        Assert.Equal(30, disk.Load(original.Role.Value.RoleId).Role.Value.X);
        writes.Clear();
        server.PublishRolePosition(40, original.Role.Value.Y);
        server.PublishRolePosition(30, original.Role.Value.Y);
        server.Flush();
        server.Flush();
        Assert.Empty(writes);
    }

    /// <summary>某角色写入失败不阻止其他角色，失败快照保持可读并允许重试。</summary>
    [Fact]
    public void FlushFailureRetainsDirtyStateAndContinuesOtherRoles()
    {
        var disk = new RepoServerRole(_directory);
        var first = disk.Create("首个", ServerRoleGender.Male, 1);
        var second = disk.Create("另一个", ServerRoleGender.Female, 1);
        using var server = new MockServer(_directory, new BagRules(8, 5, []));
        Enter(server, first.Role.Value.RoleId);
        server.PublishRoleStatus(first.Role.Value with { Health = 10 });
        server.ResetSession(1);
        Enter(server, second.Role.Value.RoleId);
        server.PublishRoleStatus(second.Role.Value with { Health = 20 });
        using (var locked = new FileStream(disk.GetRolePath(first.Role.Value.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => server.Flush());
        Assert.Equal(100, disk.Load(first.Role.Value.RoleId).Role.Value.Health);
        Assert.Equal(20, disk.Load(second.Role.Value.RoleId).Role.Value.Health);
        server.Flush();
        Assert.Equal(10, disk.Load(first.Role.Value.RoleId).Role.Value.Health);
    }

    /// <summary>创建、删除及编号在缓存中立即可见，墓碑落地后重启不恢复或复用。</summary>
    [Fact]
    public void CachedCreationDeletionAndIdsSurviveFlush()
    {
        using var server = new MockServer(_directory, new BagRules(8, 5, []));
        server.ResetSession(1);
        var replies = new List<Google.Protobuf.IMessage>();
        server.Outgoing += packet => replies.Add(MessageCodec.ReadPayload(MessageCodec.ReadEnvelope(packet), false));
        Send(server, new Login { Username = "demo", Password = "demo" });
        Send(server, new CreateRole { Name = "首个", HeadId = 1, Gender = RoleGenderPb.Male });
        var first = Assert.Single(replies.OfType<CreateRoleRt>().Last().Roles).RoleId;
        Send(server, new DeleteRole { RoleId = first });
        Assert.True(replies.OfType<DeleteRoleRt>().Last().Success);
        Send(server, new CreateRole { Name = "另一个", HeadId = 1, Gender = RoleGenderPb.Female });
        var second = Assert.Single(replies.OfType<CreateRoleRt>().Last().Roles).RoleId;
        Assert.True(second > first);
        Assert.False(Directory.Exists(_directory));
        server.Flush();
        var disk = new RepoServerRole(_directory);
        Assert.Equal(second, Assert.Single(disk.List()).Role.Value.RoleId);
        Assert.True(disk.Create("重启创建", ServerRoleGender.Male, 1).Role.Value.RoleId > second);
    }

    /// <summary>即使未选角也按周期落地；0 禁用，调整重新计时，关闭强制补写。</summary>
    [Fact]
    public void TimerWorksWithoutSelectionAndZeroDisablesUntilClose()
    {
        var interval = 5;
        var server = new MockServer(_directory, new BagRules(8, 5, []), flushInterval: () => interval);
        using var transport = new MockTransport(server);
        transport.ResetSession(1);
        transport.Send(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 1, true));
        transport.Send(MessageCodec.Encode(new CreateRole { Name = "定时测试", HeadId = 1, Gender = RoleGenderPb.Male }, 1, true));
        server.Tick(4.9);
        Assert.False(Directory.Exists(_directory));
        server.Tick(0.1);
        Assert.Single(Directory.GetFiles(_directory, "*.role.json"));
        interval = 0;
        transport.Send(MessageCodec.Encode(new CreateRole { Name = "禁用测试", HeadId = 1, Gender = RoleGenderPb.Male }, 1, true));
        server.Tick(100);
        Assert.Single(Directory.GetFiles(_directory, "*.role.json"));
        interval = 10;
        server.Tick(9);
        Assert.Single(Directory.GetFiles(_directory, "*.role.json"));
        server.Tick(1);
        Assert.Equal(2, Directory.GetFiles(_directory, "*.role.json").Length);
        interval = 0;
        transport.Send(MessageCodec.Encode(new CreateRole { Name = "关闭测试", HeadId = 1, Gender = RoleGenderPb.Male }, 1, true));
        server.ResetSession(2);
        server.Tick(100);
        Assert.Equal(2, Directory.GetFiles(_directory, "*.role.json").Length);
        transport.Dispose();
        Assert.Equal(3, new RepoServerRole(_directory).List().Count);
    }

    /// <summary>验证 openspec/specs/server-write-back 的离角显式落地：周期未到时仍写盘，重置会话后可读。</summary>
    [Fact]
    public void LeavingRoleFlushesBeforeSessionReset()
    {
        using var eventBus = new EventBusService();
        var server = new MockServer(_directory, new BagRules(8, 5, []), flushInterval: () => 0);
        using var net = new NetService(new MockTransport(server), eventBus, new LogService(Console.WriteLine));
        net.Send(new Login { Username = "demo", Password = "demo" });
        net.Tick(0);
        net.Send(new CreateRole { Name = "离角落地", HeadId = 1, Gender = RoleGenderPb.Male });
        Assert.False(Directory.Exists(_directory));

        net.FlushServerWrites();
        net.ResetSession();
        Assert.Equal("离角落地", Assert.Single(new RepoServerRole(_directory).List()).Role.Value.Name);
    }

    /// <summary>通过正式协议进入角色 Actor。</summary>
    private static void Enter(MockServer server, long id)
    {
        server.ResetSession(1);
        Send(server, new Login { Username = "demo", Password = "demo" });
        Send(server, new SelectRole { RoleId = id });
    }

    /// <summary>使用当前默认连接代次发送测试消息。</summary>
    private static void Send(MockServer server, Google.Protobuf.IMessage request) => server.Receive(MessageCodec.Encode(request, 1, true));

    /// <summary>移除本次验收独占文件。</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
