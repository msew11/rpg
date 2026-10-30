using System.Text.Json;
using Rpg.ProtocolV2;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Protocol;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-account 的统一编号、预留持久化、故障和耗尽场景。</summary>
public sealed class RoleIdGeneratorTests : IDisposable
{
    /// <summary>每个用例独占编号存储目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-role-ids-" + Guid.NewGuid());
    /// <summary>独立预留上界文件，测试不构造账号或角色摘要。</summary>
    private string SequencePath => Path.Combine(_directory, "role_id_sequence.json");

    /// <summary>大量并发跨越多个预留段，所有返回值唯一且首次从 1003 连续递增。</summary>
    [Fact]
    public async Task ConcurrentRequestsAcrossBlocksAreUnique()
    {
        var ids = new RoleIdGenerator(_directory);
        var allocated = await Task.WhenAll(Enumerable.Range(0, 2000).Select(_ => Task.Run(async () => await ids.NextAsync())));
        Assert.Equal(Enumerable.Range(1003, 2000).Select(id => (long)id), allocated.Order());
        Assert.True(JsonSerializer.Deserialize<long>(await File.ReadAllTextAsync(SequencePath)) >= allocated.Max());
    }

    /// <summary>模拟未保存摘要直接重建生成器，跳过未使用预留段且多个实例不共享已预留段。</summary>
    [Fact]
    public async Task RestartSkipsReservedIdsWithoutAnyAccountFiles()
    {
        var original = new RoleIdGenerator(_directory);
        var first = await original.NextAsync();
        var reserved = JsonSerializer.Deserialize<long>(await File.ReadAllTextAsync(SequencePath));
        var restarted = new RoleIdGenerator(_directory);
        var second = await restarted.NextAsync();
        Assert.Equal(1003, first);
        Assert.Equal(reserved + 1, second);
        Assert.Equal(first + 1, await original.NextAsync());
        Assert.Empty(Directory.GetFiles(_directory, "*.role_summary.json"));
    }

    /// <summary>损坏或非法上界不得回退到新序列，也不能改写原文件。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("broken")]
    [InlineData("1001")]
    [InlineData("-1")]
    public async Task InvalidSequenceFailsWithoutReset(string content)
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SequencePath, content);
        await Assert.ThrowsAnyAsync<Exception>(() => new RoleIdGenerator(_directory).NextAsync().AsTask());
        Assert.Equal(content, await File.ReadAllTextAsync(SequencePath));
    }

    /// <summary>预留文件无法替换时不发号，故障修复后从未使用的初始编号继续。</summary>
    [Fact]
    public async Task FailedReservationCanRetryWithoutIssuingId()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SequencePath, "1002");
        var ids = new RoleIdGenerator(_directory);
        using (var locked = new FileStream(SequencePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => ids.NextAsync().AsTask());
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal("1002", await File.ReadAllTextAsync(SequencePath));
        Assert.Equal(1003, await ids.NextAsync());
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    /// <summary>另一实例持有预留锁时拒绝取号，释放后能正常重试。</summary>
    [Fact]
    public async Task ExclusiveReservationLockPreventsCompetingWrites()
    {
        Directory.CreateDirectory(_directory);
        var ids = new RoleIdGenerator(_directory);
        using (var lease = new FileStream(SequencePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => ids.NextAsync().AsTask());
        Assert.Equal(1003, await ids.NextAsync());
    }

    /// <summary>最后一个 int64 编号可发放一次，后续和重启均明确拒绝而不回绕。</summary>
    [Fact]
    public async Task ExhaustionDoesNotOverflowOrRestart()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(SequencePath, (long.MaxValue - 1).ToString());
        var ids = new RoleIdGenerator(_directory);
        Assert.Equal(long.MaxValue, await ids.NextAsync());
        var error = await Assert.ThrowsAsync<ServerRequestException>(() => ids.NextAsync().AsTask());
        Assert.Equal(ErrorCodePb.InvalidRequest, error.Code);
        await Assert.ThrowsAsync<ServerRequestException>(() => new RoleIdGenerator(_directory).NextAsync().AsTask());
    }

    /// <summary>只清理用例自己建立的目录。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
