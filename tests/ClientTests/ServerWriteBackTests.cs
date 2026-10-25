using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;

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
        cache.SaveRole(original with { X = 20 });
        cache.SaveRole(original with { X = 30 });
        Assert.Equal(original, disk.Load(original.RoleId));
        Assert.Equal(30, cache.Load(original.RoleId).X);
        Assert.Empty(writes);
        cache.Flush();
        Assert.Equal(disk.GetRolePath(original.RoleId), Assert.Single(writes));
        Assert.Equal(30, disk.Load(original.RoleId).X);
        writes.Clear();
        cache.SaveRole(original with { X = 40 });
        cache.SaveRole(original with { X = 30 });
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
        cache.SaveRole(first with { Health = 10 });
        cache.SaveRole(second with { Health = 20 });
        using (var locked = new FileStream(disk.GetRolePath(first.RoleId), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<AggregateException>(() => cache.Flush());
        Assert.Equal(10, cache.Load(first.RoleId).Health);
        Assert.Equal(100, disk.Load(first.RoleId).Health);
        Assert.Equal(20, disk.Load(second.RoleId).Health);
        cache.Flush();
        Assert.Equal(10, disk.Load(first.RoleId).Health);
    }

    /// <summary>创建、删除及编号在缓存中立即可见，墓碑落地后重启不恢复或复用。</summary>
    [Fact]
    public void CachedCreationDeletionAndIdsSurviveFlush()
    {
        var cache = new RepoServerRole(_directory, deferWrites: true);
        var first = cache.Create("首个", ServerRoleGender.Male, 1);
        cache.Delete(first.RoleId);
        var second = cache.Create("另一个", ServerRoleGender.Female, 1);
        Assert.True(second.RoleId > first.RoleId);
        Assert.Equal(second, Assert.Single(cache.List()));
        Assert.Empty(Directory.GetFiles(_directory));
        cache.Flush();
        var disk = new RepoServerRole(_directory);
        Assert.Equal(second, Assert.Single(disk.List()));
        Assert.True(disk.Create("重启创建", ServerRoleGender.Male, 1).RoleId > second.RoleId);
    }

    /// <summary>即使未选角也按周期落地；0 禁用，调整重新计时，关闭强制补写。</summary>
    [Fact]
    public void TimerWorksWithoutSelectionAndZeroDisablesUntilClose()
    {
        var interval = 5;
        var server = new MockServer(_directory, new DataServerBagRules(8, 5, []), flushInterval: () => interval);
        using var transport = new MockTransport(server);
        transport.ResetSession(1);
        transport.Send(MessageCodec.Encode(new Login { Username = "demo", Password = "demo" }, 1, true));
        transport.Send(MessageCodec.Encode(new CreateRole { Name = "定时测试", HeadId = 1, Gender = RoleGenderPb.Male }, 1, true));
        server.Tick(4.9);
        Assert.Empty(Directory.GetFiles(_directory));
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

    /// <summary>移除本次验收独占文件。</summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
