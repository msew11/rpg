using Rpg.Services;
using Rpg.Const;
using Rpg.Session.Data;
using System.Text.Json;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Data;
using Rpg.Session;

namespace ClientTests;

/// <summary>验证 openspec/specs/mock-server、client-session 的真实请求、落盘、推送与生命周期。</summary>
public sealed class MockServerAcceptanceTests : EventServiceTestScope
{
    /// <summary>验证 client-session：登录只提供独立摘要，选角才加载最新的完整世界状态。</summary>
    [Fact]
    public void LoginReturnsSummariesAndSelectionLoadsLatestWorldState()
    {
        using var fixture = new NetworkTestFixture(Events);
        LoginRt? loginResponse = null;
        using var loginListener = Events.Subscribe<LoginRt>(response => loginResponse = response);
        var login = fixture.Login();
        Assert.True(login.Success, login.Error);
        Assert.Equal(2, login.Roles.Count);
        Assert.Equal(RoleSummaryPb.Descriptor, LoginRt.Descriptor.FindFieldByName("roles").MessageType);
        Assert.Equal(new[] { "role_id", "name", "level", "gender" },
            RoleSummaryPb.Descriptor.Fields.InFieldNumberOrder().Select(field => field.Name));
        var summary = login.Roles.Single(role => role.RoleId == 1001);
        Assert.IsType<RoleSummaryPbData>(summary);
        loginResponse!.Roles[0].Name = "篡改登录消息";
        Assert.Equal("晨星", summary.Name);

        var repo = new RepoServerRole(fixture.DirectoryPath);
        var updated = repo.Load(summary.RoleId) with { Health = 42, X = 123, Y = 456, Revision = 7 };
        repo.Save(updated);
        SelectRoleRt? selectionResponse = null;
        using var selectionListener = Events.Subscribe<SelectRoleRt>(response => selectionResponse = response);
        using var session = fixture.SelectRole(summary.RoleId);
        Assert.IsType<RoleWorldStatePb>(selectionResponse!.State);
        Assert.IsType<RoleWorldStatePbData>(session.RoleCache.State);
        Assert.Equal(updated.Health, session.Role.Health);
        Assert.Equal(new Godot.Vector2(updated.X, updated.Y), session.Role.Position);
        Assert.Equal(updated.Revision, session.RoleCache.State.Revision);
        Assert.Equal(summary.Name, session.Role.Name);
        Assert.Equal(summary.Level, session.Role.Level);
        Assert.Equal(summary.Gender, session.Role.Gender);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>验证 client-session：选角、移动和推送均复制为缓存数据，修改协议不会污染新旧快照。</summary>
    [Theory]
    [InlineData("select")]
    [InlineData("move")]
    [InlineData("notice")]
    public void CachedWorldDataDoesNotRetainProtocolObjects(string source)
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        RoleWorldStatePb? initialMessage = null;
        using var selected = Events.Subscribe<SelectRoleRt>(response => initialMessage = response.State.Clone());
        using var session = fixture.SelectRole(1001);
        selected.Dispose();
        var previous = session.RoleCache.State;
        var message = initialMessage!.Clone();
        message.Name = "更新角色";
        message.Health = 42;
        message.Gender = RoleGenderPb.Female;
        message.X = 123;
        message.Y = 456;
        message.Revision++;
        message.AcknowledgedAction = 1;
        object response = source switch
        {
            "select" => new SelectRoleRt { Success = true, RoleId = 1001, State = message },
            "move" => new MoveRt { Success = true, RoleId = 1001, ActionId = 1, State = message },
            _ => new RoleStateNotice { State = message }
        };
        Events.Publish(response);
        var cached = session.RoleCache.State;
        Assert.NotSame(previous, cached);
        Assert.Equal("更新角色", cached.Name);
        Assert.Equal(42, cached.Health);
        Assert.Equal(RoleGender.Female, cached.Gender);
        Assert.Equal(new Godot.Vector2(123, 456), cached.Position);
        Assert.Equal(previous.Revision + 1, cached.Revision);
        Assert.Equal(1, cached.AcknowledgedAction);

        message.Name = "篡改协议";
        message.Health = 0;
        message.Gender = RoleGenderPb.Male;
        message.X = message.Y = 0;
        message.Revision = message.AcknowledgedAction = 999;
        Assert.Same(cached, session.RoleCache.State);
        Assert.Equal("更新角色", cached.Name);
        Assert.Equal(42, cached.Health);
        Assert.Equal(RoleGender.Female, cached.Gender);
        Assert.Equal(new Godot.Vector2(123, 456), cached.Position);
        Assert.Equal(previous.Revision + 1, cached.Revision);
        Assert.Equal(1, cached.AcknowledgedAction);
        Assert.Equal("晨星", previous.Name);
        Assert.Equal(initialMessage.Health, previous.Health);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>成功确认发生前不改变存档；响应回调时已落盘，服务端重建恢复且不污染其他角色。</summary>
    [Fact]
    public void ConfirmedMovementPersistsBeforeResponseAndSurvivesRestart()
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var otherRole = repo.Load(1002);
        using var session = fixture.SelectRole(1001);
        var initial = session.RoleCache.State;
        var replies = 0;
        using var response = Events.Subscribe<MoveRt>(message =>
        {
            Assert.True(message.Success, message.Error);
            replies++;
            var persisted = repo.Load(1001);
            Assert.Equal(message.State.Revision, persisted.Revision);
            Assert.Equal(message.State.X, persisted.X);
            Assert.Equal(message.State.AcknowledgedAction, persisted.AcknowledgedAction);
            Assert.Equal(message.State.X, session.RoleCache.State.Position.X);
        });
        session.SubmitMove(1, session.Role.Position + new Godot.Vector2(100, 0));
        fixture.Net.Tick(0.2);
        Assert.Equal(0, replies);
        Assert.Equal(initial.Position.X, repo.Load(1001).X);
        fixture.Net.Tick(0.3);
        Assert.Equal(1, replies);
        var confirmed = session.RoleCache.State;
        session.Dispose();
        fixture.Net.Dispose();
        using var restarted = new NetworkTestFixture(Events, fixture.DirectoryPath);
        Assert.True(restarted.Login().Success);
        using var restored = restarted.SelectRole(1001);
        Assert.Equal(confirmed.Position, restored.Role.Position);
        Assert.Equal(confirmed.Revision, restored.RoleCache.State.Revision);
        Assert.Equal(0, restored.RoleCache.State.AcknowledgedAction);
        Assert.Equal(otherRole, repo.Load(1002));
        restored.SubmitMove(1, restored.Role.Position + Godot.Vector2.One);
        restarted.Net.Tick(0.5);
        Assert.Equal(confirmed.Revision + 1, restored.RoleCache.State.Revision);
        Assert.Equal(1, restored.RoleCache.State.AcknowledgedAction);
    }

    /// <summary>先注册的 UI 读到推送更新后的缓存和存档；修改协议对象不会污染客户端快照。</summary>
    [Fact]
    public void NoticeCommitsBeforeEarlierUiAndDoesNotShareMutableData()
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        SessionService? session = null;
        var notices = 0;
        var repo = new RepoServerRole(fixture.DirectoryPath);
        using var ui = Events.Subscribe<RoleStateNotice>(notice =>
        {
            notices++;
            Assert.Equal(50, session!.RoleCache.State.Health);
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
        Assert.Equal(1, session.RoleCache.State.Revision);
        Assert.Equal("晨星", repo.Load(1001).Name);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>旧版本、其他角色和失败响应不改缓存；格式错误的成功响应阻止普通监听。</summary>
    [Fact]
    public void RoleCacheRejectsOldForeignAndMalformedStates()
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        RoleWorldStatePb? selectedState = null;
        using var selectedResponse = Events.Subscribe<SelectRoleRt>(message => selectedState = message.State.Clone());
        using var session = fixture.SelectRole(1001);
        var update = selectedState!.Clone();
        update.Revision = 2;
        update.Health = 50;
        Events.Publish(new RoleStateNotice { State = update });
        var current = session.RoleCache.State;
        Events.Publish(new RoleStateNotice { State = selectedState });
        Events.Publish(new RoleStateNotice { State = new RoleWorldStatePb { RoleId = 1002, Revision = 3 } });
        Events.Publish(new MoveRt { RoleId = 1001, Error = "保存失败" });
        Assert.Same(current, session.RoleCache.State);
        var ordinaryCalls = 0;
        using var normal = Events.Subscribe<MoveRt>(_ => ordinaryCalls++);
        Events.Publish(new MoveRt { Success = true, RoleId = 1001, ActionId = 1 });
        Assert.Equal(0, ordinaryCalls);
        Assert.Single(fixture.Errors);
        Assert.Same(current, session.RoleCache.State);
    }

    /// <summary>离开角色取消在途移动，同账号重新选同角色不会接收旧确认或复用旧快照。</summary>
    [Fact]
    public void ReenteringSameRoleDiscardsDelayedWorkAndOldSubscriptions()
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        using var first = fixture.SelectRole(1001);
        var initial = first.RoleCache.State;
        first.SubmitMove(1, first.Role.Position + new Godot.Vector2(100, 0));
        fixture.Net.Tick(0.2);
        first.LeaveRole();
        Assert.False(first.RoleCache.IsInitialized);
        Assert.Throws<InvalidOperationException>(() => first.SubmitMove(2, Godot.Vector2.Zero));
        using var second = fixture.SelectRole(1001);
        var replies = 0;
        using var response = Events.Subscribe<MoveRt>(_ => replies++);
        fixture.Net.Tick(1);
        Assert.Equal(0, replies);
        Assert.Equal(initial.Position, second.Role.Position);
        Assert.Same(first, second);
        Assert.NotSame(initial, second.RoleCache.State);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        fixture.Server.PublishRoleState(repo.Load(1001) with { Health = 20 });
        fixture.Net.Tick(0);
        Assert.NotEqual(20, initial.Health);
        Assert.Equal(20, second.Role.Health);
    }

    /// <summary>已入队但未发送的移动在离开角色时丢弃。</summary>
    [Fact]
    public void LeavingSessionDiscardsUnsentMovement()
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        using var first = fixture.SelectRole(1001);
        var initial = first.Role.Position;
        first.SubmitMove(1, initial + Godot.Vector2.One);
        first.LeaveRole();
        using var second = fixture.SelectRole(1001);
        fixture.Net.Tick(1);
        Assert.Equal(0, second.RoleCache.State.Revision);
        Assert.Equal(initial, second.Role.Position);
    }

    /// <summary>无效登录不创建数据文件；未登录选角和不存在角色都返回失败响应。</summary>
    [Fact]
    public void InvalidLoginAndSelectionDoNotInitializeCaches()
    {
        using var fixture = new NetworkTestFixture(Events);
        Assert.False(fixture.Login("demo", "wrong").Success);
        Assert.Null(fixture.Session);
        Assert.False(Directory.Exists(fixture.DirectoryPath));
        SelectRoleRt? response = null;
        using var listener = Events.Subscribe<SelectRoleRt>(message => response = message);
        fixture.Net.Send(new SelectRole { RoleId = 1001 });
        fixture.Net.Tick(0);
        Assert.False(response!.Success);
        Assert.Null(fixture.Session);
        fixture.Login();
        using var missing = fixture.Session!;
        missing.SelectRole(9999);
        fixture.Net.Tick(0);
        Assert.False(response!.Success);
        Assert.Contains("角色不存在", response.Error);
        Assert.False(missing.RoleCache.IsInitialized);
    }

    /// <summary>JSON 损坏不静默初始化；登录及选角均可观察错误且保留原文件。</summary>
    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("{}")]
    public void CorruptFilesFailWithoutOverwriting(string corrupted)
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        var path = new RepoServerRole(fixture.DirectoryPath).GetRolePath(1001);
        File.WriteAllText(path, corrupted);
        using (var pending = fixture.Session!)
        {
            SelectRoleRt? selection = null;
            using var listener = Events.Subscribe<SelectRoleRt>(response => selection = response);
            pending.SelectRole(1001);
            fixture.Net.Tick(0);
            Assert.False(selection!.Success);
            Assert.Contains(path, selection.Error);
            Assert.False(pending.RoleCache.IsInitialized);
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
        using var fixture = new NetworkTestFixture(Events);
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
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var path = repo.GetRolePath(1001);
        var original = File.ReadAllText(path);
        var initial = session.RoleCache.State;
        MoveRt? response = null;
        using var listener = Events.Subscribe<MoveRt>(message => response = message);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            session.SubmitMove(1, session.Role.Position + new Godot.Vector2(100, 0));
            fixture.Net.Tick(0.5);
            Assert.False(response!.Success);
            Assert.Contains("保存失败", response.Error);
            Assert.Same(initial, session.RoleCache.State);
            Assert.Equal(original, File.ReadAllText(path));
            Assert.Empty(Directory.GetFiles(fixture.DirectoryPath, "*.tmp"));
        }
        session.SubmitMove(2, session.Role.Position + new Godot.Vector2(100, 0));
        fixture.Net.Tick(0.5);
        Assert.True(response!.Success, response.Error);
        Assert.Equal(1, session.RoleCache.State.Revision);
        Assert.Equal(2, session.RoleCache.State.AcknowledgedAction);
    }

    /// <summary>主动更新保存失败时不发送 Notice，后续更新仍从最后确认的 revision 递增。</summary>
    [Fact]
    public void FailedNoticeSaveDoesNotPublishOrCommit()
    {
        using var fixture = new NetworkTestFixture(Events);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var original = repo.Load(1001);
        var notices = 0;
        using var listener = Events.Subscribe<RoleStateNotice>(_ => notices++);
        using (var locked = new FileStream(repo.GetRolePath(1001), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => fixture.Server.PublishRoleState(original with { Health = 20 }));
        fixture.Net.Tick(0);
        Assert.Equal(0, notices);
        Assert.Equal(original.Health, session.Role.Health);
        Assert.Equal(original, repo.Load(1001));
        fixture.Server.PublishRoleState(original with { Health = 30 });
        fixture.Net.Tick(0);
        Assert.Equal(1, notices);
        Assert.Equal(1, session.RoleCache.State.Revision);
    }
}
