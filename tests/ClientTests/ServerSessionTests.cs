using Rpg.Const;
using Rpg.Net;
using Rpg.Protocol;
using Rpg.Services;
using Rpg.Tests;

namespace ClientTests;

/// <summary>验证 openspec/specs/server-account 的客户端缓存优先、在途限制与失败原子性。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerSessionTests : IDisposable
{
    /// <summary>每例服务组拥有独立数据目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-server-session-" + Guid.NewGuid());

    /// <summary>同一客户端只发一个账号操作，普通监听读取已完成的不可变缓存。</summary>
    [Fact]
    public void AccountOperationsProtocolAndCacheBeforeOrdinaryListeners()
    {
        using var services = GameServicesTestFactory.Register(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        var loggedIn = false;
        using var login = GameServices.EventBus.Subscribe<LoginRt>(_ => loggedIn = session.IsLoggedIn);
        session.Login(" DEMO1 ", "123");
        Assert.True(session.AccountRequestPending);
        Assert.Throws<InvalidOperationException>(() => session.Login("demo1", "123"));
        PumpUntil(() => !session.AccountRequestPending);
        Assert.True(loggedIn);
        var createdInCache = false;
        using var created = GameServices.EventBus.Subscribe<CreateRoleRt>(reply =>
        {
            createdInCache = session.Roles.Count == reply.Roles.Count;
            reply.Roles[0].Name = "不能修改缓存";
        });
        session.CreateRole("缓存角色", RoleGender.Female, 17);
        Assert.Throws<InvalidOperationException>(() => session.DeleteRole(1003));
        Assert.Throws<InvalidOperationException>(() => session.CreateRole("重复提交", RoleGender.Male, 9));
        PumpUntil(() => !session.AccountRequestPending);
        Assert.True(createdInCache);
        var summary = Assert.Single(session.Roles);
        Assert.Equal("缓存角色", summary.Name);
        Assert.Equal(summary.ToPb(), Rpg.Session.Data.RoleSummaryPbData.FromPb(summary.ToPb()).ToPb());
        session.SelectRole(summary.RoleId);
        Assert.Throws<InvalidOperationException>(() => session.SelectRole(summary.RoleId));
        PumpUntil(() => !session.AccountRequestPending);
        Assert.True(session.HasRole);
        Assert.True(session.HasMap);
        Assert.False(GameServices.World.HasWorld);
        session.LeaveRole();
        session.DeleteRole(summary.RoleId);
        PumpUntil(() => !session.AccountRequestPending);
        Assert.Empty(session.Roles);
    }

    /// <summary>非法完整快照不通知 UI、不替换原列表，统一失败和网络错误都解除等待。</summary>
    [Fact]
    public void MalformedSnapshotPreservesListAndAllowsRetry()
    {
        using var services = GameServicesTestFactory.Register(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        session.Login("demo1", "123");
        PumpUntil(() => !session.AccountRequestPending);
        var cache = session.LoginCache;
        var notifications = 0;
        var failures = new List<Exception>();
        GameServices.EventBus.Error += failures.Add;
        using var observed = GameServices.EventBus.Subscribe<CreateRoleRt>(_ => notifications++);
        var valid = new RoleSummaryPb { RoleId = 42, Name = "完整摘要", Level = 1, Gender = RoleGenderPb.Male, HeadId = 9 };
        cache.BeginRequest<CreateRoleRt>();
        GameServices.EventBus.Publish(new CreateRoleRt { Roles = { valid } });
        var previous = session.Roles;
        var badGender = valid.Clone();
        badGender.Gender = RoleGenderPb.Unspecified;
        var badLevel = valid.Clone();
        badLevel.Level = 0;
        foreach (var reply in new[] { new CreateRoleRt { Roles = { badGender } },
                     new CreateRoleRt { Roles = { valid, badLevel } }, new CreateRoleRt { Roles = { valid, valid.Clone() } } })
        {
            cache.BeginRequest<CreateRoleRt>();
            GameServices.EventBus.Publish(reply);
            Assert.Same(previous, session.Roles);
            Assert.False(session.AccountRequestPending);
        }
        Assert.Equal(1, notifications);
        Assert.Equal(3, failures.Count);
        cache.BeginRequest<DeleteRoleRt>();
        GameServices.EventBus.Publish(new ErrorRt { Code = ErrorCodePb.RoleNotFound, Message = "角色不存在" });
        Assert.Same(previous, session.Roles);
        Assert.False(session.AccountRequestPending);
        cache.BeginRequest<DeleteRoleRt>();
        session.CancelAccountRequest();
        Assert.False(session.AccountRequestPending);
        session.Login("demo1", "123");
        GameServices.EventBus.Publish(new LoginRt { Roles = { valid, badLevel } });
        Assert.False(session.IsLoggedIn);
        Assert.False(session.AccountRequestPending);
        Assert.Empty(session.Roles);
    }

    /// <summary>返回登录推进代次并清缓存，已完成旧请求的输出也不能建立新账号。</summary>
    [Fact]
    public void LogoutDiscardsOldReplyAndRetryCreatesFreshCache()
    {
        using var services = GameServicesTestFactory.Register(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        session.Login("demo1", "123");
        GameServices.Net.Tick(0);
        var previous = session.LoginCache;
        session.Logout();
        for (var i = 0; i < 5; i++) GameServices.Net.Tick(0);
        Assert.False(session.IsLoggedIn);
        Assert.Empty(session.Roles);
        Assert.False(previous.IsLoggedIn);
        session.Login("demo1", "wrong");
        PumpUntil(() => !session.AccountRequestPending);
        Assert.False(session.IsLoggedIn);
        session.Login("demo1", "123");
        PumpUntil(() => !session.AccountRequestPending);
        Assert.True(session.IsLoggedIn);
        Assert.NotSame(previous, session.LoginCache);
    }

    /// <summary>验证 server-world-entry：任一领域非法不留下部分缓存，成功在普通监听之前原子接纳。</summary>
    [Fact]
    public void WorldEntryRejectsEveryMalformedPartAndCommitsBeforeUi()
    {
        using var services = GameServicesTestFactory.Register(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        session.Login("demo1", "123"); PumpUntil(() => !session.AccountRequestPending);
        session.CreateRole("原子角色", RoleGender.Male, 9); PumpUntil(() => !session.AccountRequestPending);
        var roleId = session.Roles.Single().RoleId;
        var observed = 0;
        var errors = new List<Exception>();
        GameServices.EventBus.Error += errors.Add;
        using var normal = GameServices.EventBus.Subscribe<EnterWorldRt>(_ =>
        {
            Assert.True(session.HasRole && session.HasMap);
            Assert.Equal("实际名称", session.Roles.Single().Name);
            observed++;
        });
        var baseline = session.Roles;
        foreach (var change in new Action<EnterWorldRt>[] {
            reply => reply.State = null,
            reply => reply.State.RoleId++,
            reply => reply.State.Profile.HeadId = 0,
            reply => reply.State.Status.Health = 101,
            reply => reply.Role.NextExperience = 0,
            reply => reply.Bag = null,
            reply => reply.Bag.Items.Add(new ItemPb { InstanceId = 1, ItemId = int.MaxValue, Count = 1 }),
            reply => reply.Map = null,
            reply => reply.Map.Chunks.RemoveAt(0),
            reply => reply.Map.Layout.Width++,
            reply => reply.State.Position.X = -1,
            reply => reply.Map.Center.X = 2,
            reply => reply.Map.Chunks[0].Objects.Add(new MapObjectPb { ObjectId = Guid.NewGuid().ToString("N"), Revision = 1,
                GroundItem = new() { ItemId = int.MaxValue, Count = 1 } }) })
        {
            session.SelectRole(roleId);
            Assert.Throws<InvalidOperationException>(() => session.CreateRole("重复", RoleGender.Male, 9));
            var reply = Entry(roleId); change(reply);
            GameServices.EventBus.Publish(reply);
            Assert.False(session.HasRole); Assert.False(session.HasMap); Assert.False(session.AccountRequestPending);
            Assert.Same(baseline, session.Roles);
        }
        Assert.Equal(13, errors.Count); Assert.Equal(0, observed);
        session.SelectRole(roleId);
        var valid = Entry(roleId);
        var gear = GameServices.Config.Item.Items.Values.First(item => item.MaxStack == 1 &&
            Rpg.Utils.EquipmentSlotUtil.TryGetSlot(item.Category, item.SubCategory, out var slot) && slot == EquipmentSlot.MainHand);
        valid.Bag.Items.Add(new ItemPb { InstanceId = 1, ItemId = gear.Id, Equipped = true, Count = 1, Equipment = new() });
        valid.Bag.Items.Add(new ItemPb { InstanceId = 2, ItemId = gear.Id, SlotIndex = 0, Count = 1, Equipment = new() });
        valid.State.Equipment.EquippedItems.Add(gear.Id);
        valid.Map.Chunks[0].Objects.Add(new MapObjectPb { ObjectId = Guid.NewGuid().ToString("N"), Revision = 2,
            GroundItem = new() { ItemId = gear.Id, Count = 1 } });
        valid.Map.Chunks[1].Objects.Add(new MapObjectPb { ObjectId = Guid.NewGuid().ToString("N"), Revision = 4, X = 1024,
            Monster = new() { MonsterTypeId = "初始怪物" } });
        GameServices.EventBus.Publish(valid);
        Assert.Single(session.Bag.Items); Assert.Single(session.Bag.Equipment); Assert.Single(session.Equipment.Items);
        Assert.Equal(2, session.Map!.Objects.Count);
        Assert.Equal(1, observed);
        valid.State.Profile.Name = "外部修改";
        Assert.Equal("实际名称", session.Role.Name);
        Assert.Equal("实际名称", session.Roles.Single().Name);
        var state = session.Role; var map = session.Map; var bag = session.Bag; var summaries = session.Roles;
        session.SelectRole(roleId);
        var invalid = Entry(roleId); invalid.Map.Chunks.Clear();
        GameServices.EventBus.Publish(invalid);
        Assert.Same(state, session.Role); Assert.Same(map, session.Map); Assert.Same(bag, session.Bag); Assert.Same(summaries, session.Roles);
        session.LeaveRole();
        Assert.False(session.HasRole); Assert.False(session.HasMap);
        Assert.Same(summaries, session.Roles);
        Assert.Equal(0, session.RoleId);
    }

    /// <summary>验证 server-world-entry：真实进入在途返回登录隔离旧结果，物品写入仅发送 协议。</summary>
    [Fact]
    public async Task EntryGenerationAndItemWritesAreIsolated()
    {
        using var services = GameServicesTestFactory.Register(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        session.Login("demo1", "123"); PumpUntil(() => !session.AccountRequestPending);
        session.CreateRole("只读角色", RoleGender.Female, 17); PumpUntil(() => !session.AccountRequestPending);
        var roleId = session.Roles.Single().RoleId;
        session.SelectRole(roleId); GameServices.Net.Tick(0);
        await GameServices.MockServer.FlushAsync();
        session.Logout();
        for (var i = 0; i < 3; i++) GameServices.Net.Tick(0);
        Assert.False(session.HasRole); Assert.Empty(session.Roles);
        session.Login("demo1", "123"); PumpUntil(() => !session.AccountRequestPending);
        session.SelectRole(roleId); PumpUntil(() => !session.AccountRequestPending);
        Assert.True(session.HasRole && session.HasMap);
        GameServices.World.InitializeRole();
        var original = GameServices.World.RolePosition;
        using var click = new Godot.InputEventMouseButton { ButtonIndex = Godot.MouseButton.Left, Pressed = true };
        PumpUntil(() => session.CanMove);
        Assert.True(GameServices.World.HandleInput(click, original + Godot.Vector2.One * 100));
        GameServices.World.Tick(.01);
        Assert.NotEqual(original, GameServices.World.RolePosition);
        session.SendGm("item 1012"); PumpUntil(() => session.Bag.Items.Count == 1);
        var item = session.Bag.Items.Single();
        session.MoveItem(item.InstanceId, 2); PumpUntil(() => session.Bag.Items.Single().SlotIndex == 2);
        Assert.Equal(1, session.Bag.Items.Single().Count);

        Assert.False(session.ResyncMapIfNeeded());
        GameServices.Net.FlushServerWrites();
        GameServices.World.LeaveRole(); session.LeaveRole();
        Assert.False(GameServices.World.HasWorld); Assert.False(session.HasRole);
    }

    /// <summary>对应 server-items：完整物品按序接纳，非法装备不得部分覆盖背包或摘要。</summary>
    [Fact]
    public void ItemNoticeValidatesBothHalvesBeforeCommit()
    {
        using var services = GameServicesTestFactory.Register(ConfigTestFixture.CreateEnv(_directory, Path.Combine(_directory, "server")));
        var session = GameServices.Session;
        session.Login("demo1", "123"); PumpUntil(() => !session.AccountRequestPending);
        session.CreateRole("物品原子缓存", RoleGender.Male, 1); PumpUntil(() => !session.AccountRequestPending);
        session.SelectRole(session.Roles.Single().RoleId); PumpUntil(() => session.CanMove);
        var valid = new RoleItemsChangedNotice { RoleId = session.RoleId, Bag = new(), Equipment = new() };
        valid.Bag.Items.Add(new ItemPb { InstanceId = 1, ItemId = 1012, SlotIndex = 0, Count = 1, Equipment = new() });
        GameServices.EventBus.Publish(valid);
        var bag = session.Bag; var equipment = session.Equipment; var summaries = session.Roles;
        var invalid = valid.Clone(); invalid.Bag.Items[0].SlotIndex = 3; invalid.Equipment.EquippedItems.Add(999999);
        var failures = new List<Exception>(); GameServices.EventBus.Error += failures.Add;
        GameServices.EventBus.Publish(invalid);
        Assert.Single(failures); Assert.Same(bag, session.Bag); Assert.Same(equipment, session.Equipment); Assert.Same(summaries, session.Roles);
        valid.Bag.Items[0].SlotIndex = 4; GameServices.EventBus.Publish(valid);
        Assert.Equal(4, session.Bag.Items.Single().SlotIndex);
    }

    /// <summary>构造正式完整初始响应，四个边缘空块须全部提供。</summary>
    private static EnterWorldRt Entry(long roleId) => new()
    {
        State = new() { RoleId = roleId, Profile = new() { Name = "实际名称", Level = 1, Gender = RoleGenderPb.Male, HeadId = 9 },
            Status = new() { Health = 100, MaxHealth = 100, Mana = 60, MaxMana = 60 }, Position = new() { MapId = 1 }, Equipment = new() },
        Role = new() { NextExperience = 500 }, Bag = new(),
        Map = new() { MapId = 1, Layout = new() { Width = 1000, Height = 1000, TileSize = 32, ChunkSizeTiles = 32 }, Center = new(),
            Chunks = { new MapChunkPb { Coordinate = new() }, new MapChunkPb { Coordinate = new() { X = 1 } },
                new MapChunkPb { Coordinate = new() { Y = 1 } }, new MapChunkPb { Coordinate = new() { X = 1, Y = 1 } } } }
    };

    /// <summary>推进正式固定帧传输，等待后台 Actor 回复但不阻塞网络收取。</summary>
    private static void PumpUntil(Func<bool> complete)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!complete() && DateTime.UtcNow < deadline) { GameServices.Net.Tick(0); Thread.Sleep(1); }
        Assert.True(complete(), "账号请求未完成");
    }
    /// <summary>仅删除测试创建的临时数据。</summary>
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
