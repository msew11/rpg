using Rpg.ProtocolV2;
using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Login.Data;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.Role.Storage;
using Rpg.ServerV2.Modules.Role;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Protocol;
using Rpg.ServerV2.Runtime;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-storage 的真实文件、显式屏障和关闭补写集成。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerV2WriteBackTests : ServerV2ModuleTests
{
    /// <summary>本例独占存档目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-v2-write-back-" + Guid.NewGuid());
    /// <summary>显式保存和关闭都等待已接纳但尚未修改内存的请求。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedRequestCompletesBeforeExplicitOrFinalSave(bool closing)
    {
        var entered = ServerV2PersistenceActorTests.Signal();
        var release = ServerV2PersistenceActorTests.Signal();
        var repo = new RepoAccountRoleSummary(_directory);
        var modules = new ServerModules(Config, repo, new DoubleServerV2Repo(), _ => new ActorHandlers<LoginContext>()
            .Register<Login, LoginRt>(new DoubleMutatingLoginHandler(entered, release.Task)));
        await using var server = new ServerRuntime(modules);
        var connection = server.OpenConnection();
        var work = server.ReceiveAsync(connection, LoginPacket());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var saving = closing ? server.StopAsync() : server.FlushAsync();
        Assert.False(saving.IsCompleted);
        Assert.False(Directory.Exists(_directory));
        release.SetResult();
        await Task.WhenAll(work, saving).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("已接纳修改", (await repo.LoadAsync("alice")).Roles[0].Name);
        if (!closing)
        {
            await server.ReceiveAsync(connection, LoginPacket());
            Assert.Equal("alice", server.Session(connection).Username);
        }
    }
    /// <summary>失败释放仍停止分片且不丢回滚文件，成功角色继续提交。</summary>
    [Fact]
    public async Task ShutdownReportsFailureButStopsActorsAndPersistsIndependentAccount()
    {
        var repo = new ServerV2PersistenceActorTests.DoubleRoleRepo { FailSave = true };
        var accounts = new RepoAccountRoleSummary(_directory);
        var modules = new ServerModules(Config, accounts, repo, _ => ServerV2PersistenceActorTests.LoginHandlers(),
            _ => ServerV2PersistenceActorTests.RoleHandlers());
        var server = new ServerRuntime(modules);
        var role = modules.Role(10);
        var login = modules.Login("alice");
        await role.Ask(new ServerV2PersistenceActorTests.DataWriteRole(ServerV2StorageTests.Sample() with { Role = new() { Name = "待写" } }));
        await login.Ask(new ServerV2PersistenceActorTests.DataWriteSummary("保存成功"));
        await Assert.ThrowsAsync<AggregateException>(() => server.StopAsync());
        Assert.Equal(ActorState.STOP, role.State);
        Assert.Equal(ActorState.STOP, login.State);
        Assert.Equal("保存成功", (await accounts.LoadAsync("alice")).Roles[0].Name);
        Assert.Throws<ObjectDisposedException>(() => server.OpenConnection());
    }
    /// <summary>磁盘缺表导致 INIT 失败，修复后同目录重建 Actor 并完整恢复三表。</summary>
    [Fact]
    public async Task RepairingMissingFileAllowsActorReinitializationAndRoleRoundTrip()
    {
        var repo = new RepoRole(_directory);
        await repo.SaveAsync(10, ServerV2StorageTests.Sample(), DataRoleTables.All);
        File.Delete(Path.Combine(_directory, "10.item.json"));
        await using var modules = new ServerModules(Config, new DoubleServerV2Repo(), repo, _ => new(), _ => ServerV2PersistenceActorTests.RoleHandlers());
        var failed = modules.Role(10);
        await Assert.ThrowsAsync<FileNotFoundException>(() => failed.Ask(new ServerV2PersistenceActorTests.DataReadRole()));
        await File.WriteAllTextAsync(Path.Combine(_directory, "10.item.json"), "[]");
        var actor = modules.Role(10);
        Assert.NotSame(failed, actor);
        var loaded = await actor.Ask(new ServerV2PersistenceActorTests.DataReadRole());
        var changed = loaded with
        {
            Role = loaded.Role with { Name = "新角色", NextInstanceId = 2 },
            Items = [new(1, 999, 0, 1)], Equipments = [new() { InstanceId = 1 }]
        };
        Assert.Equal(DataRoleTables.All, await actor.Ask(new ServerV2PersistenceActorTests.DataWriteRole(changed)));
        await modules.FlushAsync();
        var restored = await repo.LoadAsync(10);
        Assert.Equal(changed.Role, restored.Role);
        Assert.Equal(changed.Items.ToArray(), restored.Items.ToArray());
        Assert.Equal(changed.Equipments.ToArray(), restored.Equipments.ToArray());
    }
    /// <summary>序列化测试请求，不增添正式业务协议。</summary>
    internal static byte[] LoginPacket() => MessageCodec.Encode(new Login { Username = " Alice ", Password = "unused" }, 1, 1, true);
    /// <summary>存储链路测试 Handler，只修改 Context。</summary>
    internal sealed class DoubleMutatingLoginHandler(TaskCompletionSource? entered = null, Task? release = null)
        : IActorHandler<LoginContext, Login, LoginRt>
    {
        /// <summary>可控阻塞后提交摘要，用于验证请求与保存的先后顺序。</summary>
        public async ValueTask<LoginRt> HandleAsync(LoginContext context, Login request)
        {
            entered?.TrySetResult();
            if (release is not null) await release.ConfigureAwait(false);
            context.ReplaceSummaries(new(context.Username, [new(1, "已接纳修改", 1, Rpg.ServerV2.Modules.Common.Data.RoleGender.Male, 1, [])]));
            return ServerV2NetworkTests.LoginResponse(context);
        }
    }
    /// <summary>仅移除本例创建的独立临时文件。</summary>
    public override void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); base.Dispose(); }
}
