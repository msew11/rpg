using Rpg.ServerV2.Hosting;
using Rpg.ServerV2.Modules.Common.Data;
using Rpg.ServerV2.Modules.Login.Data;
using Rpg.ServerV2.Modules.Login.Storage;
using Rpg.ServerV2.Modules.Login;
using Rpg.ServerV2.Modules.Role.Data;
using Rpg.ServerV2.Modules.Role.Storage;
using Rpg.ServerV2.Modules.Role;
using Rpg.ServerV2.Modules;
using Rpg.ServerV2.Runtime;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-v2-storage 的 INIT 屏障、保存回合、分片隔离与重试。</summary>
public sealed class ServerV2PersistenceActorTests
{
    /// <summary>加载完成前不执行 Handler，其他角色分片不受影响；未启动分片不被 Flush 激活。</summary>
    [Fact]
    public async Task InitializationWaitsForLoadAndOtherShardsProgressWithoutStartingUnusedOnFlush()
    {
        var release = Signal();
        var repo = new DoubleRoleRepo { LoadRelease = release.Task };
        await using var modules = Modules(repo);
        var actor = modules.Role(10);
        var unused = modules.Role(99);
        Assert.Equal(0, repo.LoadCount);
        var first = actor.Ask(new DataReadRole());
        await repo.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var changed = Sample("更新");
        var next = actor.Ask(new DataWriteRole(changed));
        Assert.False(first.IsCompleted);
        Assert.False(next.IsCompleted);
        Assert.Equal(11, (await modules.Role(11).Ask(new DataReadRole()).WaitAsync(TimeSpan.FromSeconds(5))).Role.RoleId);
        release.SetResult();
        Assert.Equal("原角色", (await first).Role.Name);
        Assert.Equal(DataRoleTables.Role, await next);
        Assert.Equal("更新", (await actor.Ask(new DataReadRole())).Role.Name);
        await modules.FlushAsync();
        Assert.Equal(2, repo.LoadCount);
        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(ActorState.INIT, unused.State);
    }
    /// <summary>INIT 错误拒绝所有等待者，下一次取得目录分片可以重建并重新加载。</summary>
    [Fact]
    public async Task FailedInitializationRejectsWaitersAndDirectoryAllowsReload()
    {
        var release = Signal();
        var repo = new DoubleRoleRepo { FailLoad = true, LoadRelease = release.Task };
        await using var modules = Modules(repo);
        var first = modules.Role(10);
        var one = first.Ask(new DataReadRole());
        await repo.LoadEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var two = first.Ask(new DataReadRole());
        release.SetResult();
        await Assert.ThrowsAsync<IOException>(() => one);
        await Assert.ThrowsAsync<IOException>(() => two);
        repo.FailLoad = false;
        var retry = modules.Role(10);
        Assert.NotSame(first, retry);
        Assert.Equal(10, (await retry.Ask(new DataReadRole())).Role.RoleId);
        Assert.Equal(2, repo.LoadCount);
    }
    /// <summary>保存期间本角色下一回合等待，其他分片继续；失败保留最新内存及待写标记。</summary>
    [Fact]
    public async Task SavingOwnsMailboxAndFailedSaveCanRetryWithoutLosingMemory()
    {
        var release = Signal();
        var repo = new DoubleRoleRepo { SaveRelease = release.Task, FailSave = true };
        await using var modules = Modules(repo);
        var actor = modules.Role(10);
        await actor.Ask(new DataWriteRole(Sample("保留")));
        var flushing = actor.Ask(new DataFlush());
        await repo.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var read = actor.Ask(new DataReadRole());
        Assert.False(read.IsCompleted);
        await modules.Role(11).Ask(new DataReadRole()).WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        await Assert.ThrowsAsync<IOException>(() => flushing);
        Assert.Equal("保留", (await read).Role.Name);
        Assert.Equal(DataRoleTables.Role, await actor.Ask(new DataWriteRole(Sample("保留"))));
        repo.FailSave = false;
        await actor.Ask(new DataFlush());
        Assert.Equal("保留", repo.LastSaved!.Role.Name);
        Assert.Equal(DataRoleTables.None, await actor.Ask(new DataWriteRole(Sample("保留"))));
        await actor.Ask(new DataFlush());
        Assert.Equal(2, repo.SaveCount);
    }
    /// <summary>失败分片不阻止同组独立角色和后续账号保存，最终汇总失败。</summary>
    [Fact]
    public async Task FlushContinuesOtherRolesAndAccountsWhenOneRoleFails()
    {
        var repo = new DoubleRoleRepo { FailSave = true };
        var accounts = new DoubleAccountRepo();
        await using var modules = new ServerModules(accounts, repo, _ => LoginHandlers(), _ => RoleHandlers());
        await modules.Role(10).Ask(new DataWriteRole(Sample("失败角色")));
        await modules.Role(11).Ask(new DataWriteRole(Sample("成功角色")));
        var account = modules.Login("alice");
        await account.Ask(new DataWriteSummary("更新摘要"));
        var error = await Assert.ThrowsAsync<AggregateException>(() => modules.FlushAsync());
        Assert.Single(error.InnerExceptions);
        Assert.Equal(2, repo.SaveCount);
        Assert.Equal("成功角色", repo.LastSaved!.Role.Name);
        Assert.Equal(1, accounts.SaveCount);
        repo.FailSave = false;
        await modules.FlushAsync();
        Assert.Equal(3, repo.SaveCount);
        Assert.Equal(1, accounts.SaveCount);
    }
    /// <summary>文件加载后的摘要进入 Handler，修改写回后新 Actor 可恢复；原值恢复不写。</summary>
    [Fact]
    public async Task LoginLoadsAndPersistsOnlyChangedSummaries()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-v2-login-" + Guid.NewGuid());
        try
        {
            var repo = new RepoAccountRoleSummary(directory);
            await repo.SaveAsync(new("alice", [new(1, "文件摘要", 1, RoleGender.Male, 1, [])]));
            await using (var actor = new LoginActor("alice", repo, LoginHandlers()))
            {
                Assert.Equal("文件摘要", await actor.Ref.Ask(new DataReadSummary()));
                await actor.Ref.Ask(new DataWriteSummary("新摘要"));
                await actor.Ref.Ask(new DataWriteSummary("文件摘要"));
                var path = Path.Combine(directory, "alice.role_summary.json");
                File.SetLastWriteTimeUtc(path, new DateTime(2000, 1, 1));
                await actor.Ref.Ask(new DataFlush());
                Assert.Equal(2000, File.GetLastWriteTimeUtc(path).Year);
                await actor.Ref.Ask(new DataWriteSummary("新摘要"));
                await actor.Ref.Ask(new DataFlush());
            }
            await using var reopened = new LoginActor("alice", repo, LoginHandlers());
            Assert.Equal("新摘要", await reopened.Ref.Ask(new DataReadSummary()));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    /// <summary>构造独立测试模块，不引入真实业务。</summary>
    private static ServerModules Modules(IRoleRepo repo) => new(new DoubleServerV2Repo(), repo, _ => LoginHandlers(), _ => RoleHandlers());
    /// <summary>每个角色单独冻结其读写处理器表。</summary>
    internal static ActorHandlers<RoleContext> RoleHandlers() => new ActorHandlers<RoleContext>()
        .Register<DataReadRole, DataRoleSnapshot>(new DoubleReadRoleHandler()).Register<DataWriteRole, DataRoleTables>(new DoubleWriteRoleHandler());
    /// <summary>账号测试只操作摘要内存。</summary>
    internal static ActorHandlers<LoginContext> LoginHandlers() => new ActorHandlers<LoginContext>()
        .Register<DataWriteSummary, bool>(new DoubleWriteSummaryHandler()).Register<DataReadSummary, string>(new DoubleReadSummaryHandler());
    /// <summary>独立信号避免测试延续内联进入 Actor 回合。</summary>
    internal static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>构造带名称变化的快照。</summary>
    private static DataRoleSnapshot Sample(string name) => ServerV2StorageTests.Sample() with { Role = new() { RoleId = 10, Name = name } };
    /// <summary>只读请求返回不可变数据，不暴露 Context。</summary>
    internal sealed record DataReadRole : IActorRequest<DataRoleSnapshot>;
    /// <summary>测试写入消息只携带不可变三表。</summary>
    internal sealed record DataWriteRole(DataRoleSnapshot Snapshot) : IActorRequest<DataRoleTables>;
    /// <summary>摘要测试消息只携带展示名称。</summary>
    internal sealed record DataWriteSummary(string Name) : IActorRequest<bool>;
    /// <summary>摘要只读请求。</summary>
    internal sealed record DataReadSummary : IActorRequest<string>;
    /// <summary>角色快照读取契约的替身。</summary>
    private sealed class DoubleReadRoleHandler : IActorHandler<RoleContext, DataReadRole, DataRoleSnapshot>
    {
        /// <summary>返回不可变快照。</summary>
        public ValueTask<DataRoleSnapshot> HandleAsync(RoleContext context, DataReadRole request) => ValueTask.FromResult(context.Snapshot());
    }
    /// <summary>验证 Handler 统一提交入口的替身。</summary>
    private sealed class DoubleWriteRoleHandler : IActorHandler<RoleContext, DataWriteRole, DataRoleTables>
    {
        /// <summary>只修改内存，返回待写表。</summary>
        public ValueTask<DataRoleTables> HandleAsync(RoleContext context, DataWriteRole request)
        { context.Commit(request.Snapshot); return ValueTask.FromResult(context.DirtyTables); }
    }
    /// <summary>账号摘要修改契约的替身。</summary>
    private sealed class DoubleWriteSummaryHandler : IActorHandler<LoginContext, DataWriteSummary, bool>
    {
        /// <summary>通过 Context 更新摘要，不访问仓库。</summary>
        public ValueTask<bool> HandleAsync(LoginContext context, DataWriteSummary request)
        { context.ReplaceSummaries(new(context.Username, [new(1, request.Name, 1, RoleGender.Male, 1, [])])); return ValueTask.FromResult(context.SummariesDirty); }
    }
    /// <summary>账号已加载摘要读取契约的替身。</summary>
    private sealed class DoubleReadSummaryHandler : IActorHandler<LoginContext, DataReadSummary, string>
    {
        /// <summary>返回首个摘要名称。</summary>
        public ValueTask<string> HandleAsync(LoginContext context, DataReadSummary request) => ValueTask.FromResult(context.Summaries.Roles[0].Name);
    }
    /// <summary>提供可控加载、保存阻塞及错误的角色仓库契约替身。</summary>
    internal sealed class DoubleRoleRepo : IRoleRepo
    {
        /// <summary>角色 10 的加载阻塞。</summary>
        internal Task? LoadRelease { get; init; }
        /// <summary>角色 10 的保存阻塞。</summary>
        internal Task? SaveRelease { get; init; }
        /// <summary>已进入角色 10 加载。</summary>
        internal TaskCompletionSource LoadEntered { get; } = Signal();
        /// <summary>已进入角色 10 保存。</summary>
        internal TaskCompletionSource SaveEntered { get; } = Signal();
        /// <summary>角色 10 是否模拟加载失败。</summary>
        internal bool FailLoad { get; set; }
        /// <summary>角色 10 是否模拟保存失败。</summary>
        internal bool FailSave { get; set; }
        /// <summary>跨分片加载总数。</summary>
        internal int LoadCount;
        /// <summary>跨分片保存总数。</summary>
        internal int SaveCount;
        /// <summary>最近成功保存的不可变数据。</summary>
        internal DataRoleSnapshot? LastSaved;
        /// <summary>只对指定角色注入阻塞和错误。</summary>
        public async ValueTask<DataRoleSnapshot> LoadAsync(long roleId)
        {
            Interlocked.Increment(ref LoadCount);
            if (roleId == 10)
            {
                LoadEntered.TrySetResult();
                if (LoadRelease is not null) await LoadRelease;
                if (FailLoad) throw new IOException("模拟加载失败");
            }
            return new(new() { RoleId = roleId, Name = "原角色" }, [], []);
        }
        /// <summary>成功才记录数据，错误由所属 Actor 处理。</summary>
        public async ValueTask SaveAsync(long roleId, DataRoleSnapshot snapshot, DataRoleTables tables)
        {
            Interlocked.Increment(ref SaveCount);
            if (roleId == 10)
            {
                SaveEntered.TrySetResult();
                if (SaveRelease is not null) await SaveRelease;
                if (FailSave) throw new IOException("模拟保存失败");
            }
            LastSaved = snapshot;
        }
    }
    /// <summary>统计账号写回次数的摘要仓库替身。</summary>
    private sealed class DoubleAccountRepo : IAccountRoleSummaryRepo
    {
        /// <summary>写回次数。</summary>
        internal int SaveCount;
        /// <summary>每个账号从空摘要开始。</summary>
        public ValueTask<DataAccountRoleSummaries> LoadAsync(string username) => ValueTask.FromResult(new DataAccountRoleSummaries(username, []));
        /// <summary>统计保存，不执行文件 IO。</summary>
        public ValueTask SaveAsync(DataAccountRoleSummaries summaries) { Interlocked.Increment(ref SaveCount); return ValueTask.CompletedTask; }
    }
}
