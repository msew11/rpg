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
        var cache = new RepoServerRole(_directory, (path, _) => writes.Add(path), deferWrites: true);
        cache.SaveRole(original.WithRole(original.Role.Value with { X = 20 }));
        cache.SaveRole(original.WithRole(original.Role.Value with { X = 30 }));
        Assert.Equal(original, disk.Load(original.Role.Value.RoleId));
        Assert.Equal(30, cache.Load(original.Role.Value.RoleId).Role.Value.X);
        Assert.Empty(writes);
        cache.Flush();
        Assert.Equal(disk.GetRolePath(original.Role.Value.RoleId), Assert.Single(writes));
        Assert.Equal(30, disk.Load(original.Role.Value.RoleId).Role.Value.X);
        writes.Clear();
        cache.SaveRole(original.WithRole(original.Role.Value with { X = 40 }));
        cache.SaveRole(original.WithRole(original.Role.Value with { X = 30 }));
        cache.SaveBag(original);
        cache.Flush();
        cache.Flush();
        Assert.Empty(writes);
    }

    /// <summary>某角色写入失败不阻止其他角色，失败快照保持可读并允许重试。</summary>
    [Fact]
    public void FlushFailureRetainsDirtyStateAndContinuesOtherRoles()
    {
        var disk = new RepoServerRole(_directory);
        var first = disk.Create("首个", ServerRoleGender.Male, 1);
        var second = disk.Create("另一个", ServerRoleGender.Female, 1);
        var cache = new RepoServerRole(_directory, deferWrites: true);
        cache.SaveRole(first.WithRole(first.Role.Value with { Health = 10 }));
        cache.SaveRole(second.WithRole(second.Role.Value with { Health = 20 }));
        using (var locked = new FileStream(disk.GetRolePath(first.Role.Value.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => cache.Flush());
        Assert.Equal(10, cache.Load(first.Role.Value.RoleId).Role.Value.Health);
        Assert.Equal(100, disk.Load(first.Role.Value.RoleId).Role.Value.Health);
        Assert.Equal(20, disk.Load(second.Role.Value.RoleId).Role.Value.Health);
        cache.Flush();
        Assert.Equal(10, disk.Load(first.Role.Value.RoleId).Role.Value.Health);
    }

    /// <summary>创建、删除及编号在缓存中立即可见，墓碑落地后重启不恢复或复用。</summary>
    [Fact]
    public void CachedCreationDeletionAndIdsSurviveFlush()
    {
        var cache = new RepoServerRole(_directory, deferWrites: true);
        var first = cache.Create("首个", ServerRoleGender.Male, 1);
        cache.Delete(first.Role.Value.RoleId);
        var second = cache.Create("另一个", ServerRoleGender.Female, 1);
        Assert.True(second.Role.Value.RoleId > first.Role.Value.RoleId);
        Assert.Equal(second, Assert.Single(cache.List()));
        Assert.False(Directory.Exists(_directory));
        cache.Flush();
        var disk = new RepoServerRole(_directory);
        Assert.Equal(second, Assert.Single(disk.List()));
        Assert.True(disk.Create("重启创建", ServerRoleGender.Male, 1).Role.Value.RoleId > second.Role.Value.RoleId);
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

    /// <summary>移除本次验收独占文件。</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
