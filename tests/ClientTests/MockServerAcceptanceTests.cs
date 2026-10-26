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
[Trait("Runtime", "Godot")]
public sealed class MockServerAcceptanceTests : RegisteredServiceTestScope
{
    /// <summary>验证 client-session：登录只提供独立摘要，选角才加载最新的完整世界状态。</summary>
    [Fact]
    public void LoginReturnsSummariesAndSelectionLoadsLatestWorldState()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var updated = repo.Load(1001) with { Health = 42, X = 123, Y = 456, Revision = 7 };
        repo.Save(updated);
        LoginRt? loginResponse = null;
        using var loginListener = EventBus.Subscribe<LoginRt>(response => loginResponse = response);
        var login = fixture.Login();
        Assert.True(login.Success, login.Error);
        Assert.Equal(2, login.Roles.Count);
        Assert.Equal(RoleSummaryPb.Descriptor, LoginRt.Descriptor.FindFieldByName("roles").MessageType);
        Assert.Equal(new[] { "role_id", "name", "level", "gender", "head_id" },
            RoleSummaryPb.Descriptor.Fields.InFieldNumberOrder().Select(field => field.Name));
        var summary = login.Roles.Single(role => role.RoleId == 1001);
        Assert.IsType<RoleSummaryPbData>(summary);
        loginResponse!.Roles[0].Name = "篡改登录消息";
        Assert.Equal("晨星", summary.Name);

        SelectRoleRt? selectionResponse = null;
        using var selectionListener = EventBus.Subscribe<SelectRoleRt>(response => selectionResponse = response);
        using var session = fixture.SelectRole(summary.RoleId);
        Assert.IsType<RoleWorldStatePb>(selectionResponse!.State);
        Assert.IsType<RolePb>(selectionResponse.Role);
        Assert.Equal(updated.Experience, selectionResponse.Role.Experience);
        Assert.Equal(updated.NextExperience, selectionResponse.Role.NextExperience);
        Assert.Null(RoleWorldStatePb.Descriptor.FindFieldByName("role"));
        var worldJson = Google.Protobuf.JsonFormatter.Default.Format(selectionResponse.State);
        Assert.DoesNotContain("experience", worldJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bagExtraRows", worldJson, StringComparison.OrdinalIgnoreCase);
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
    [InlineData("notice")]
    public void CachedWorldDataDoesNotRetainProtocolObjects(string source)
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        RoleWorldStatePb? initialMessage = null;
        using var selected = EventBus.Subscribe<SelectRoleRt>(response => initialMessage = response.State.Clone());
        using var session = fixture.SelectRole(1001);
        selected.Dispose();
        var previous = session.RoleCache.State;
        var message = initialMessage!.Clone();
        message.Profile.Name = "更新角色";
        message.Status.Health = 42;
        message.Profile.Gender = RoleGenderPb.Female;
        message.Position.X = 123;
        message.Position.Y = 456;
        message.Profile.Revision++;
        message.Status.Revision++;
        message.Position.Revision++;
        message.Position.AcknowledgedAction = 1;
        object response = source switch
        {
            "select" => new SelectRoleRt { Success = true, RoleId = 1001, State = message,
                Role = session.Role.ToRolePb(), Bag = session.Bag.ToPb() },
            _ => new RoleStateNotice { State = message }
        };
        EventBus.Publish(response);
        var cached = session.RoleCache.State;
        Assert.NotSame(previous, cached);
        Assert.Equal("更新角色", cached.Name);
        Assert.Equal(42, cached.Health);
        Assert.Equal(RoleGender.Female, cached.Gender);
        Assert.Equal(new Godot.Vector2(123, 456), cached.Position);
        Assert.Equal(previous.Revision + 1, cached.Revision);
        Assert.Equal(1, cached.AcknowledgedAction);

        message.Profile.Name = "篡改协议";
        message.Status.Health = 0;
        message.Profile.Gender = RoleGenderPb.Male;
        message.Position.X = message.Position.Y = 0;
        message.Profile.Revision = message.Status.Revision = message.Position.Revision = message.Position.AcknowledgedAction = 999;
        Assert.Same(cached, session.RoleCache.State);
        Assert.Equal("更新角色", cached.Name);
        Assert.Equal(42, cached.Health);
        Assert.Equal(RoleGender.Female, cached.Gender);
        Assert.Equal(new Godot.Vector2(123, 456), cached.Position);
        Assert.Equal(previous.Revision + 1, cached.Revision);
        Assert.Equal(1, cached.AcknowledgedAction);
        Assert.Equal("晨星", previous.Name);
        Assert.Equal(initialMessage.Status.Health, previous.Health);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>验证 client-session：资料和位置交错到达时各自过滤版本，移动响应不覆盖资料。</summary>
    [Fact]
    public void PartialRoleUpdatesMergeByComponentRevision()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var initial = session.Role.ToPb();
        var profile = initial.Profile.Clone();
        profile.Revision = 2;
        profile.Name = "更新资料";
        EventBus.Publish(new RoleProfileNotice { Profile = profile });
        var position = initial.Position.Clone();
        position.Revision = 3;
        position.X += 40;
        EventBus.Publish(new RolePositionNotice { Position = position });
        Assert.Equal("更新资料", session.Role.Name);
        Assert.Equal(position.X, session.Role.Position.X);
        Assert.Equal(2, session.Role.ProfileRevision);
        Assert.Equal(3, session.Role.PositionRevision);

        var oldFull = initial.Clone();
        oldFull.Profile.Revision = 1;
        oldFull.Position.Revision = 1;
        EventBus.Publish(new RoleStateNotice { State = oldFull });
        Assert.Equal("更新资料", session.Role.Name);
        Assert.Equal(position.X, session.Role.Position.X);

        var moved = position.Clone();
        moved.Revision = 4;
        moved.AcknowledgedAction = 1;
        moved.Y += 20;
        EventBus.Publish(new MoveRt { Success = true, RoleId = 1001, ActionId = 1, Position = moved });
        Assert.Equal("更新资料", session.Role.Name);
        Assert.Equal(moved.Y, session.Role.Position.Y);
        profile.Name = "篡改旧协议";
        position.X = 0;
        Assert.Equal("更新资料", session.Role.Name);
        Assert.NotEqual(0, session.Role.Position.X);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>验证 client-session：可见状态和私有 Role 独立合并，旧世界快照不回滚玩法数据。</summary>
    [Fact]
    public void StatusAndPrivateRoleUpdatesStaySeparate()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var oldWorld = session.Role.ToPb();
        var status = oldWorld.Status.Clone();
        status.Revision = 2;
        status.Health = 40;
        EventBus.Publish(new RoleStatusNotice { Status = status });
        var role = session.Role.ToRolePb();
        role.Revision = 3;
        role.Experience += 10;
        role.BagExtraRows = 1;
        EventBus.Publish(new RoleChangedNotice { Role = role });
        Assert.Equal(40, session.Role.Health);
        Assert.Equal(role.Experience, session.Role.Experience);
        Assert.Equal(1, session.Role.BagExtraRows);
        Assert.Equal(2, session.Role.StatusRevision);
        Assert.Equal(3, session.Role.RoleRevision);
        EventBus.Publish(new RoleStateNotice { State = oldWorld });
        Assert.Equal(40, session.Role.Health);
        Assert.Equal(role.Experience, session.Role.Experience);
        Assert.Equal(1, session.Role.BagExtraRows);
        Assert.Null(RoleWorldProfilePb.Descriptor.FindFieldByName("experience"));
        role.Experience = 0;
        status.Health = 0;
        Assert.NotEqual(0, session.Role.Experience);
        Assert.Equal(40, session.Role.Health);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>验证 client-session：选角缺失或错配私有领域时不提交任何角色快照。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectionRequiresMatchingPrivateRole(bool wrongRole)
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var state = session.Role.ToPb();
        session.RoleCache.Reset(1001);
        EventBus.Publish(new SelectRoleRt { Success = true, RoleId = 1001, State = state,
            Role = wrongRole ? new RolePb { RoleId = 1002, NextExperience = 100 } : null,
            Bag = new BagPb() });
        Assert.False(session.HasRole);
        Assert.Single(fixture.Errors);
    }

    /// <summary>验证 mock-server：状态与私有领域只发对应推送，完整世界快照不携带私有数据。</summary>
    [Fact]
    public void ServerSeparatesStatusAndPrivateRoleNotices()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var original = new RepoServerRole(fixture.DirectoryPath).Load(1001);
        var statuses = 0;
        var roles = 0;
        var full = 0;
        using var statusListener = EventBus.Subscribe<RoleStatusNotice>(_ => statuses++);
        using var roleListener = EventBus.Subscribe<RoleChangedNotice>(_ => roles++);
        using var fullListener = EventBus.Subscribe<RoleStateNotice>(_ => full++);
        fixture.Server.PublishRoleStatus(original with { Health = 50 });
        fixture.Net.Tick(0);
        Assert.Equal(50, session.Role.Health);
        Assert.Equal(original.Experience, session.Role.Experience);
        fixture.Server.PublishRole(original with { Experience = original.Experience + 5, BagExtraRows = 1 });
        fixture.Net.Tick(0);
        Assert.Equal(original.Experience + 5, session.Role.Experience);
        Assert.Equal(1, session.Role.BagExtraRows);
        Assert.Equal(1, statuses);
        Assert.Equal(1, roles);
        Assert.Equal(0, full);
        Assert.Equal(original.Name, session.Role.Name);
        fixture.Server.PublishRoleState(original with { Experience = original.Experience + 12,
            Health = 45, BagExtraRows = 1 });
        fixture.Net.Tick(0);
        Assert.Equal(45, session.Role.Health);
        Assert.Equal(original.Experience + 12, session.Role.Experience);
        Assert.Equal(2, roles);
        Assert.Equal(1, full);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>验证 mock-server：主动资料、位置及普通移动使用各自消息且不重复推送。</summary>
    [Fact]
    public void ServerSendsSeparateProfileAndPositionNotices()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var profiles = 0;
        var positions = 0;
        var full = 0;
        using var profileListener = EventBus.Subscribe<RoleProfileNotice>(_ => profiles++);
        using var positionListener = EventBus.Subscribe<RolePositionNotice>(_ => positions++);
        using var fullListener = EventBus.Subscribe<RoleStateNotice>(_ => full++);
        var old = session.Role;
        fixture.Server.PublishRoleProfile(new RepoServerRole(fixture.DirectoryPath).Load(1001) with { Name = "新名字" });
        fixture.Net.Tick(0);
        Assert.Equal("新名字", session.Role.Name);
        Assert.Equal(old.Position, session.Role.Position);
        fixture.Server.PublishRolePosition(old.Position.X + 25, old.Position.Y);
        fixture.Net.Tick(0);
        Assert.Equal(old.Position.X + 25, session.Role.Position.X);
        Assert.Equal(1, profiles);
        Assert.Equal(1, positions);
        Assert.Equal(0, full);
        session.SubmitMove(1, session.Role.Position + new Godot.Vector2(50, 0));
        fixture.Net.Tick(0.5);
        Assert.Equal(1, positions);
        Assert.Equal(0, full);
        Assert.Equal(1, session.Role.AcknowledgedAction);
        Assert.Empty(fixture.Errors);
    }

    /// <summary>server-write-back：响应时内存已确认而文件未变，正常关闭补写后重启恢复且隔离角色。</summary>
    [Fact]
    public void ConfirmedMovementFlushesOnCloseAndSurvivesRestart()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var otherRole = repo.Load(1002);
        using var session = fixture.SelectRole(1001);
        var initial = session.RoleCache.State;
        var replies = 0;
        using var response = EventBus.Subscribe<MoveRt>(message =>
        {
            Assert.True(message.Success, message.Error);
            replies++;
            var persisted = repo.Load(1001);
            Assert.True(message.Position.Revision > persisted.Revision);
            Assert.Equal(initial.Position.X, persisted.X);
            Assert.Equal(message.Position.X, session.RoleCache.State.Position.X);
        });
        session.SubmitMove(1, session.Role.Position + new Godot.Vector2(100, 0));
        fixture.Net.Tick(0.2);
        Assert.Equal(0, replies);
        Assert.Equal(initial.Position.X, repo.Load(1001).X);
        fixture.Net.Tick(0.3);
        Assert.Equal(1, replies);
        var confirmed = session.RoleCache.State;
        Assert.Empty(fixture.Errors);
        response.Dispose();
        session.Dispose();
        fixture.Net.Dispose();
        using var restarted = new NetworkTestFixture(EventBus, fixture.DirectoryPath);
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

    /// <summary>server-write-back：UI 读到更新缓存而磁盘仍为旧值；修改协议对象不污染快照。</summary>
    [Fact]
    public void NoticeCommitsBeforeEarlierUiAndDoesNotShareMutableData()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        SessionService? session = null;
        var notices = 0;
        var repo = new RepoServerRole(fixture.DirectoryPath);
        using var ui = EventBus.Subscribe<RoleStateNotice>(notice =>
        {
            notices++;
            Assert.Equal(50, session!.RoleCache.State.Health);
            Assert.Equal(86, repo.Load(1001).Health);
            Assert.Equal(RoleGender.Female, session.Role.Gender);
            notice.State.Profile.Name = "篡改消息对象";
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
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        RoleWorldStatePb? selectedState = null;
        using var selectedResponse = EventBus.Subscribe<SelectRoleRt>(message => selectedState = message.State.Clone());
        using var session = fixture.SelectRole(1001);
        var update = selectedState!.Clone();
        update.Profile.Revision = 2;
        update.Position.Revision = 2;
        update.Status.Health = 50;
        update.Status.Revision = 2;
        EventBus.Publish(new RoleStateNotice { State = update });
        var current = session.RoleCache.State;
        EventBus.Publish(new RoleStateNotice { State = selectedState });
        EventBus.Publish(new RoleStateNotice { State = new RoleWorldStatePb {
            Profile = new RoleWorldProfilePb { RoleId = 1002, Revision = 3 },
            Status = new RoleWorldStatusPb { RoleId = 1002, Revision = 3 },
            Position = new RoleWorldPositionPb { RoleId = 1002, Revision = 3 },
            Equipment = new RoleWorldEquipmentPb { RoleId = 1002 } } });
        EventBus.Publish(new MoveRt { RoleId = 1001, Error = "保存失败" });
        Assert.Same(current, session.RoleCache.State);
        var ordinaryCalls = 0;
        using var normal = EventBus.Subscribe<MoveRt>(_ => ordinaryCalls++);
        EventBus.Publish(new MoveRt { Success = true, RoleId = 1001, ActionId = 1 });
        Assert.Equal(0, ordinaryCalls);
        Assert.Single(fixture.Errors);
        Assert.Same(current, session.RoleCache.State);
    }

    /// <summary>离开角色取消在途移动，同账号重新选同角色不会接收旧确认或复用旧快照。</summary>
    [Fact]
    public void ReenteringSameRoleDiscardsDelayedWorkAndOldSubscriptions()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
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
        using var response = EventBus.Subscribe<MoveRt>(_ => replies++);
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
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
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
        using var fixture = new NetworkTestFixture(EventBus);
        Assert.False(fixture.Login("demo", "wrong").Success);
        Assert.Null(fixture.Session);
        Assert.False(Directory.Exists(fixture.DirectoryPath));
        SelectRoleRt? response = null;
        using var listener = EventBus.Subscribe<SelectRoleRt>(message => response = message);
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
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        var path = new RepoServerRole(fixture.DirectoryPath).GetRolePath(1001);
        File.WriteAllText(path, corrupted);
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
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
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

    /// <summary>server-write-back：定时落地失败通过网络错误上报，不回退已经确认的移动。</summary>
    [Fact]
    public void FailedMovementFlushPreservesConfirmedMemoryAndRetries()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var path = repo.GetRolePath(1001);
        var original = File.ReadAllText(path);
        MoveRt? response = null;
        using var listener = EventBus.Subscribe<MoveRt>(message => response = message);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            session.SubmitMove(1, session.Role.Position + new Godot.Vector2(100, 0));
            fixture.Net.Tick(5);
            Assert.True(response!.Success);
            Assert.Equal(1, session.RoleCache.State.Revision);
            Assert.Single(fixture.Errors);
            Assert.Equal(original, File.ReadAllText(path));
        }
        fixture.Net.Tick(5);
        Assert.Equal(session.RoleCache.State.Revision, repo.Load(1001).Revision);
    }

    /// <summary>server-write-back：主动推送只依赖内存提交，落地失败可重试而不重复增加版本。</summary>
    [Fact]
    public void FailedNoticeFlushKeepsPublishedMemory()
    {
        using var fixture = new NetworkTestFixture(EventBus, seedRoles: true);
        fixture.Login();
        using var session = fixture.SelectRole(1001);
        var repo = new RepoServerRole(fixture.DirectoryPath);
        var original = repo.Load(1001);
        var notices = 0;
        using var listener = EventBus.Subscribe<RoleStateNotice>(_ => notices++);
        using (var locked = new FileStream(repo.GetRolePath(1001), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fixture.Server.PublishRoleState(original with { Health = 20 });
            fixture.Net.Tick(5);
            Assert.Equal(1, notices);
            Assert.Equal(20, session.Role.Health);
            Assert.Equal(original, repo.Load(1001));
            Assert.Single(fixture.Errors);
        }
        fixture.Net.Tick(5);
        Assert.Equal(20, repo.Load(1001).Health);
        Assert.Equal(1, session.RoleCache.State.Revision);
        Assert.Equal(1, notices);
    }
}
