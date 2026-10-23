using System.Text.Json;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;
using Rpg.Session;

namespace ClientTests;

/// <summary>验证 openspec/specs/mock-server、client-session 的真实请求、落盘、推送与生命周期。</summary>
public sealed class MockServerAcceptanceTests
{
    /// <summary>成功确认发生前不改变存档；响应回调时已落盘，服务端重建恢复且不污染其他角色。</summary>
    [Fact]
    public void ConfirmedMovementPersistsBeforeResponseAndSurvivesRestart()
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var otherRole = repo.Load(1002);
        using var session = fixture.SelectRole(1001);
        var initial = session.Cache.Role.State;
        var replies = 0;
        using var response = session.Events.Subscribe<MoveRt>(message =>
        {
            Assert.True(message.Success, message.Error);
            replies++;
            var persisted = repo.Load(1001);
            Assert.Equal(message.State.Revision, persisted.Revision);
            Assert.Equal(message.State.X, persisted.X);
            Assert.Equal(message.State.AcknowledgedAction, persisted.AcknowledgedAction);
            Assert.Equal(message.State.X, session.Cache.Role.State.Position.X);
        });
        session.SubmitMove(1, session.Role.Position + new Godot.Vector2(100, 0));
        fixture.Net.Tick(0.2);
        Assert.Equal(0, replies);
        Assert.Equal(initial.Position.X, repo.Load(1001).X);
        fixture.Net.Tick(0.3);
        Assert.Equal(1, replies);
        var confirmed = session.Cache.Role.State;
        session.Dispose();
        fixture.Net.Dispose();
        using var restarted = new NetworkTestFixture(fixture.DirectoryPath);
        Assert.True(restarted.Login().Success);
        using var restored = restarted.SelectRole(1001);
        Assert.Equal(confirmed.Position, restored.Role.Position);
        Assert.Equal(confirmed.Revision, restored.Cache.Role.State.Revision);
        Assert.Equal(0, restored.Cache.Role.State.AcknowledgedAction);
        Assert.Equal(otherRole, repo.Load(1002));
        restored.SubmitMove(1, restored.Role.Position + Godot.Vector2.One);
        restarted.Net.Tick(0.5);
        Assert.Equal(confirmed.Revision + 1, restored.Cache.Role.State.Revision);
        Assert.Equal(1, restored.Cache.Role.State.AcknowledgedAction);
    }

    /// <summary>先注册的 UI 读到推送更新后的缓存和存档；修改协议对象不会污染客户端快照。</summary>
    [Fact]
    public void NoticeCommitsBeforeEarlierUiAndDoesNotShareMutableData()
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        GameSession? session = null;
        var notices = 0;
        var repo = new RepoServerRole(fixture.DirectoryPath);
        using var ui = fixture.Net.Events.Subscribe<RoleStateNotice>(notice =>
        {
            notices++;
            Assert.Equal(50, session!.Cache.Role.State.Role.Health);
            Assert.Equal(50, repo.Load(1001).Health);
            Assert.Equal(RoleGender.Female, session.Role.Gender);
            notice.State.Name = "篡改消息对象";
        });
        using var selected = session = fixture.SelectRole(1001);
        var update = repo.Load(1001) with { Health = 50, Gender = ServerRoleGender.Female };
        fixture.Server.PublishRoleState(update);
        Assert.Equal(0, notices);
        fixture.Net.Tick(0);
        Assert.Equal(1, notices);
        Assert.Equal("晨星", session.Role.Name);
        Assert.Equal(1, session.Cache.Role.State.Revision);
        Assert.Equal("晨星", repo.Load(1001).Name);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>旧版本、其他角色和失败响应不改缓存；格式错误的成功响应阻止普通监听。</summary>
    [Fact]
    public void RoleCacheRejectsOldForeignAndMalformedStates()
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        DataRoleMessage? selectedState = null;
        using var selectedResponse = fixture.Net.Events.Subscribe<SelectRoleRt>(message => selectedState = message.State.Clone());
        using var session = fixture.SelectRole(1001);
        var update = selectedState!.Clone();
        update.Revision = 2;
        update.Health = 50;
        fixture.Net.Events.Publish(new RoleStateNotice { State = update });
        var current = session.Cache.Role.State;
        fixture.Net.Events.Publish(new RoleStateNotice { State = selectedState });
        fixture.Net.Events.Publish(new RoleStateNotice { State = new DataRoleMessage { RoleId = 1002, Revision = 3 } });
        fixture.Net.Events.Publish(new MoveRt { RoleId = 1001, Error = "保存失败" });
        Assert.Same(current, session.Cache.Role.State);
        var ordinaryCalls = 0;
        using var normal = fixture.Net.Events.Subscribe<MoveRt>(_ => ordinaryCalls++);
        fixture.Net.Events.Publish(new MoveRt { Success = true, RoleId = 1001, ActionId = 1 });
        Assert.Equal(0, ordinaryCalls);
        Assert.Single(fixture.Errors);
        Assert.Same(current, session.Cache.Role.State);
    }

    /// <summary>结束会话取消在途移动，同角色重新进入不会接收旧确认或复用旧缓存。</summary>
    [Fact]
    public void ReenteringSameRoleDiscardsDelayedWorkAndOldSubscriptions()
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        using var first = fixture.SelectRole(1001);
        var initial = first.Cache.Role.State;
        first.SubmitMove(1, first.Role.Position + new Godot.Vector2(100, 0));
        fixture.Net.Tick(0.2);
        first.Dispose();
        using var second = fixture.SelectRole(1001);
        var replies = 0;
        using var response = fixture.Net.Events.Subscribe<MoveRt>(_ => replies++);
        fixture.Net.Tick(1);
        Assert.Equal(0, replies);
        Assert.Equal(initial.Position, second.Role.Position);
        Assert.NotSame(first.Cache, second.Cache);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        fixture.Server.PublishRoleState(repo.Load(1001) with { Health = 20 });
        fixture.Net.Tick(0);
        Assert.Same(initial, first.Cache.Role.State);
        Assert.Equal(20, second.Role.Health);
        Assert.Throws<ObjectDisposedException>(() => first.SubmitMove(2, Godot.Vector2.Zero));
    }

    /// <summary>已入队但未发送的移动在离开会话时丢弃。</summary>
    [Fact]
    public void LeavingSessionDiscardsUnsentMovement()
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        using var first = fixture.SelectRole(1001);
        var initial = first.Role.Position;
        first.SubmitMove(1, initial + Godot.Vector2.One);
        first.Dispose();
        using var second = fixture.SelectRole(1001);
        fixture.Net.Tick(1);
        Assert.Equal(0, second.Cache.Role.State.Revision);
        Assert.Equal(initial, second.Role.Position);
    }

    /// <summary>无效登录不创建数据文件；未登录选角和不存在角色都返回失败响应。</summary>
    [Fact]
    public void InvalidLoginAndSelectionDoNotInitializeCaches()
    {
        using var fixture = new NetworkTestFixture();
        Assert.False(fixture.Login("demo", "wrong").Success);
        Assert.False(Directory.Exists(fixture.DirectoryPath));
        SelectRoleRt? response = null;
        using var listener = fixture.Net.Events.Subscribe<SelectRoleRt>(message => response = message);
        using (var unauthenticated = new GameSession(1001, fixture.Net))
        {
            fixture.Net.Send(new SelectRole { RoleId = 1001 });
            fixture.Net.Tick(0);
            Assert.False(response!.Success);
            Assert.False(unauthenticated.Cache.Role.IsInitialized);
        }
        fixture.Login();
        using var missing = new GameSession(9999, fixture.Net);
        fixture.Net.Send(new SelectRole { RoleId = 9999 });
        fixture.Net.Tick(0);
        Assert.False(response!.Success);
        Assert.Contains("角色不存在", response.Error);
        Assert.False(missing.Cache.Role.IsInitialized);
    }

    /// <summary>JSON 损坏不静默初始化；登录及选角均可观察错误且保留原文件。</summary>
    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{}")]
    public void CorruptFilesFailWithoutOverwriting(string corrupted)
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        var path = new RepoServerRole(fixture.DirectoryPath).GetRolePath(1001);
        File.WriteAllText(path, corrupted);
        using (var pending = new GameSession(1001, fixture.Net))
        {
            SelectRoleRt? selection = null;
            using var listener = fixture.Net.Events.Subscribe<SelectRoleRt>(response => selection = response);
            fixture.Net.Send(new SelectRole { RoleId = 1001 });
            fixture.Net.Tick(0);
            Assert.False(selection!.Success);
            Assert.Contains(path, selection.Error);
            Assert.False(pending.Cache.Role.IsInitialized);
        }
        var login = fixture.Login();
        Assert.False(login.Success);
        Assert.Contains(path, login.Error);
        Assert.Equal(corrupted, File.ReadAllText(path));
    }

    /// <summary>合法 JSON 中身份或数值字段损坏也不能变成有效角色。</summary>
    [Theory]
    [InlineData("identity")]
    [InlineData("revision")]
    [InlineData("gender")]
    public void InvalidStoredFieldsAreDiagnosed(string field)
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var original = repo.Load(1001);
        var invalid = field switch
        {
            "identity" => original with { RoleId = 1002 },
            "revision" => original with { Revision = -1 },
            _ => original with { Gender = (ServerRoleGender)99 }
        };
        var json = JsonSerializer.Serialize(invalid);
        File.WriteAllText(repo.GetRolePath(1001), json);
        Assert.False(fixture.Login().Success);
        Assert.Equal(json, File.ReadAllText(repo.GetRolePath(1001)));
    }

    /// <summary>Windows 文件占用导致替换失败时不提交缓存或成功响应，解除占用后可继续保存。</summary>
    [Fact]
    public void FailedMovementSavePreservesConfirmedStateAndOriginalFile()
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var path = repo.GetRolePath(1001);
        var original = File.ReadAllText(path);
        var initial = session.Cache.Role.State;
        MoveRt? response = null;
        using var listener = fixture.Net.Events.Subscribe<MoveRt>(message => response = message);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            session.SubmitMove(1, session.Role.Position + new Godot.Vector2(100, 0));
            fixture.Net.Tick(0.5);
            Assert.False(response!.Success);
            Assert.Contains("保存失败", response.Error);
            Assert.Same(initial, session.Cache.Role.State);
            Assert.Equal(original, File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
        }
        session.SubmitMove(2, session.Role.Position + new Godot.Vector2(100, 0));
        fixture.Net.Tick(0.5);
        Assert.True(response!.Success, response.Error);
        Assert.Equal(1, session.Cache.Role.State.Revision);
        Assert.Equal(2, session.Cache.Role.State.AcknowledgedAction);
    }

    /// <summary>主动更新保存失败时不发送 Notice，后续更新仍从最后确认的 revision 递增。</summary>
    [Fact]
    public void FailedNoticeSaveDoesNotPublishOrCommit()
    {
        using var fixture = new NetworkTestFixture();
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var original = repo.Load(1001);
        var notices = 0;
        using var listener = fixture.Net.Events.Subscribe<RoleStateNotice>(_ => notices++);
        using (var locked = new FileStream(repo.GetRolePath(1001), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => fixture.Server.PublishRoleState(original with { Health = 20 }));
        fixture.Net.Tick(0);
        Assert.Equal(0, notices);
        Assert.Equal(original.Health, session.Role.Health);
        Assert.Equal(original, repo.Load(1001));
        fixture.Server.PublishRoleState(original with { Health = 30 });
        fixture.Net.Tick(0);
        Assert.Equal(1, notices);
        Assert.Equal(1, session.Cache.Role.State.Revision);
    }
}
