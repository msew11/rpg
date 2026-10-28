using System.Reflection;
using Google.Protobuf;
using Rpg.Const;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Server.Entity;
using Rpg.Server.EntityCache;
using Rpg.Server.Handler.Bag;
using Rpg.Server.Handler.Gm;
using Rpg.Server.Handler.Login;
using Rpg.Server.Handler.Role;
using Rpg.Server.Handler.World;
using Rpg.Server.Map;
using Rpg.Tests;

namespace ClientTests;

/// <summary>验证 mock-server 的请求处理与 IO 分离、原子拒绝及等待中请求场景。</summary>
public sealed class ServerHandlerTests : IDisposable
{
    /// <summary>仅本测试拥有的存档根目录。</summary>
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "rpg-handlers-" + Guid.NewGuid());
    /// <summary>显式准备完成后共享的实体入口。</summary>
    private readonly ServerRequestContext _cache;
    /// <summary>用于 IO 准备和最终落地的外层仓库。</summary>
    private readonly RepoServerRole _repo;
    /// <summary>已经加载的地图。</summary>
    private readonly ServerMap _map;
    /// <summary>本测试角色编号。</summary>
    private readonly long _id;

    /// <summary>先准备文件与缓存，Handler 不参与任何加载。</summary>
    public ServerHandlerTests()
    {
        var disk = new RepoServerRole(_directory);
        var role = disk.Create("处理器角色", ServerRoleGender.Male, 1, spawnX: 100, spawnY: 100);
        _id = role.Role.Value.RoleId;
        var rules = new BagRules(2, 2,
            [RoleTestData.Item(1012, "剑", 1, ItemCategory.Weapon, ItemSubCategory.Sword), RoleTestData.Item(2000, "堆叠", 10)]);
        _repo = new RepoServerRole(_directory);
        _map = new ServerMap(new Rpg.Server.Map.MapLayout(1, 1000, 1000, 32, 32), new RepoServerMap(_directory),
            new Dictionary<int, int> { [1012] = 1, [2000] = 10 });
        _cache = new ServerRequestContext(rules, world: new ServerWorldCache(_map));
        foreach (var summary in _repo.ReadSummaries()) _cache.Login.Summaries[summary.RoleId] = summary;
        _cache.Login.Loaded = true;
        _cache.Role.Tables = _repo.Load(_id);
    }

    /// <summary>mock-server：锁住全部角色表仍能登录、选角、发放、穿脱、整理、丢弃、拾取和重同步。</summary>
    [Fact]
    public void PreparedHandlersDoNotReadOrWriteLockedStorage()
    {
        var originals = Directory.GetFiles(_directory).ToDictionary(path => path, File.ReadAllBytes);
        var locks = originals.Keys.Select(path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        try
        {
            Enter();
            _cache.Session.ItemsRevision = 7;
            Assert.True(new GmHandler().Handle(_cache, new Gm { RoleId = _id, Command = "item 1012" }).Success);
            Assert.True(new EquipItemHandler().Handle(_cache, new EquipItem { RoleId = _id, InstanceId = 1, Slot = EquipmentSlotPb.MainHand,
                ExpectedBagRevision = 7, ExpectedEquipmentRevision = 7 }).Success);
            Assert.True(new EquipItemHandler().Handle(_cache, new EquipItem { RoleId = _id, Slot = EquipmentSlotPb.MainHand,
                ExpectedBagRevision = 7, ExpectedEquipmentRevision = 7 }).Success);
            Assert.True(new MoveItemHandler().Handle(_cache, new MoveItem { RoleId = _id, InstanceId = 1, TargetSlotIndex = 3, ExpectedRevision = 7 }).Success);
            var drop = new DropItemHandler().Handle(_cache, new DropItem { RoleId = _id, InstanceId = 1, Count = 1, ExpectedRevision = 7 });
            Assert.True(drop.Success, drop.Error);
            var ground = _map.Objects[Guid.ParseExact(drop.ObjectId, "N")];
            Assert.True(new MoveHandler().Handle(_cache, new Move { RoleId = _id, ActionId = 1, X = ground.X, Y = ground.Y }).Success);
            var request = new PickupItem { RoleId = _id, ObjectId = drop.ObjectId, ExpectedBagRevision = 7,
                ExpectedObjectRevision = ground.Revision, MoveActionId = 1 };
            Assert.True(new PickupItemHandler().Handle(_cache, request).Success);
            ServerSimulation.Advance(_cache, 1, 1000);
            Assert.Null(_cache.Session.Pickup);
            Assert.Empty(_map.Objects);
            Assert.Single(_cache.Current!.BagItems);
            Assert.True(new ResyncMapAoiHandler().Handle(_cache, new ResyncMapAoi { RoleId = _id, MapId = 1 }).Success);
            Assert.Contains(_cache.Notices, message => message is MoveRt { Success: true });
            Assert.Contains(_cache.Notices, message => message is PickupItemRt { Success: true });
            Assert.NotEqual(0, _cache.Role.Dirty);
        }
        finally { foreach (var file in locks) file.Dispose(); }
        Assert.Equal(originals.Count, Directory.GetFiles(_directory).Length);
        foreach (var (path, bytes) in originals) Assert.Equal(bytes, File.ReadAllBytes(path));
        // 外层显式提交后，角色实体与物品才落地。
        _repo.Save(_cache.Role.Tables!);
        Assert.Single(new RepoServerRole(_directory).Load(_id).BagItems);
    }

    /// <summary>mock-server：背包不足与过期版本拒绝时不更改实体、摘要、编号、地图或 dirty。</summary>
    [Fact]
    public void RejectedHandlersPreserveAllMemoryState()
    {
        Enter();
        _cache.Session.ItemsRevision = 9;
        Assert.True(new GmHandler().Handle(_cache, new Gm { RoleId = _id, Command = "item 1012 4" }).Success);
        _cache.Notices.Clear();
        var before = _cache.Current;
        var summary = _cache.Login.Summaries[_id];
        var dirty = _cache.Role.Dirty;
        Assert.False(new GmHandler().Handle(_cache, new Gm { RoleId = _id, Command = "item 1012" }).Success);
        Assert.False(new MoveItemHandler().Handle(_cache, new MoveItem { RoleId = _id, InstanceId = 1, TargetSlotIndex = 1, ExpectedRevision = 8 }).Success);
        Assert.False(new EquipItemHandler().Handle(_cache, new EquipItem { RoleId = _id, InstanceId = 1, Slot = EquipmentSlotPb.OffHand,
            ExpectedBagRevision = 9, ExpectedEquipmentRevision = 9 }).Success);
        Assert.False(new DropItemHandler().Handle(_cache, new DropItem { RoleId = _id, InstanceId = 1, Count = 2, ExpectedRevision = 9 }).Success);
        Assert.Same(before, _cache.Current);
        Assert.Same(summary, _cache.Login.Summaries[_id]);
        Assert.Equal(dirty, _cache.Role.Dirty);
        Assert.Empty(_map.Objects);
        Assert.Empty(_cache.World!.Transfers);
        Assert.Empty(_cache.Notices);
    }

    /// <summary>mock-server：选择未准备角色只返回加载错误，不能绕过外层直接读盘。</summary>
    [Fact]
    public void SelectionRequiresExplicitPreparation()
    {
        _cache.Role.Tables = null;
        Assert.True(new LoginHandler().Handle(_cache, new Login { Username = "demo", Password = "demo" }).Success);
        var response = new SelectRoleHandler().Handle(_cache, new SelectRole { RoleId = _id });
        Assert.False(response.Success);
        Assert.Null(_cache.Current);
        Assert.Empty(_cache.Notices);
        _cache.Role.Tables = _repo.Load(_id);
        Assert.True(new SelectRoleHandler().Handle(_cache, new SelectRole { RoleId = _id }).Success);
    }

    /// <summary>mock-server：创建和删除只使用已准备摘要及删除单例，不依赖未选物品表。</summary>
    [Fact]
    public void RoleListHandlersUseOnlyPreparedMemory()
    {
        _cache.Role.Tables = null;
        _cache.Login.Deletions[_id] = _repo.ReadRole(_id);
        var paths = Directory.GetFiles(_directory);
        var locks = paths.Select(path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None)).ToArray();
        try
        {
            Assert.True(new LoginHandler().Handle(_cache, new Login { Username = "demo", Password = "demo" }).Success);
            Assert.True(new DeleteRoleHandler().Handle(_cache, new DeleteRole { RoleId = _id }).Success);
            var created = new CreateRoleHandler().Handle(_cache, new CreateRole { Name = "新处理器角色", Gender = RoleGenderPb.Female, HeadId = 2 });
            Assert.True(created.Success, created.Error);
            Assert.True(Assert.Single(created.Roles).RoleId > _id);
            Assert.Null(_cache.Current);
            Assert.True(_cache.Login.Deletions[_id].IsDeleted);
        }
        finally { foreach (var file in locks) file.Dispose(); }
        _repo.SaveDeletion(_cache.Login.Deletions[_id], _cache.Login.Summaries[_id]);
        foreach (var tables in _cache.Login.Created.Values) _repo.Save(tables);
        Assert.Equal("新处理器角色", Assert.Single(new RepoServerRole(_directory).ListSummaries()).Name);
    }

    /// <summary>mock-server：走近期间重复拾取不重复响应，新有效移动取消旧拾取。</summary>
    [Fact]
    public void WaitingPickupCanRepeatAndCancelWithoutIo()
    {
        Enter();
        var ground = new MapObjectEntity { X = 500, Y = 100, ItemId = 2000, Count = 2 };
        _map.Create(ground);
        var move = new MoveHandler().Handle(_cache, new Move { RoleId = _id, ActionId = 1, X = ground.X, Y = ground.Y });
        Assert.True(move.Success);
        Assert.Null(move.Position);
        var pickup = new PickupItem { RoleId = _id, ObjectId = ground.ObjectId.ToString("N"), MoveActionId = 1, ExpectedObjectRevision = 1 };
        Assert.True(new PickupItemHandler().Handle(_cache, pickup).Success);
        Assert.True(new PickupItemHandler().Handle(_cache, pickup.Clone()).Success);
        Assert.NotNull(_cache.Session.Pickup);
        Assert.Empty(_cache.Notices);
        ServerSimulation.Advance(_cache, 0.2, 100);
        Assert.Empty(_cache.Notices);
        Assert.True(new MoveHandler().Handle(_cache, new Move { RoleId = _id, ActionId = 2, X = 200, Y = 100 }).Success);
        Assert.Null(_cache.Session.Pickup);
        Assert.IsType<PickupItemRt>(Assert.Single(_cache.Notices));
        Assert.False(((PickupItemRt)_cache.Notices.Peek()).Success);
        Assert.Single(_map.Objects);
        Assert.Equal(0, _cache.Role.Dirty);
    }

    /// <summary>mock-server：每个 protobuf Handler 仅公开一个约定签名的处理入口。</summary>
    [Fact]
    public void EveryRequestHandlerHasTheRequiredSignature()
    {
        var types = typeof(MockServer).Assembly.GetTypes().Where(type => type.Namespace?.StartsWith("Rpg.Server.Handler.") == true && type.Name.EndsWith("Handler")).ToArray();
        Assert.Equal(11, types.Length);
        foreach (var type in types)
        {
            var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal("Handle", method.Name);
            var parameters = method.GetParameters();
            Assert.Equal(2, parameters.Length);
            Assert.Equal(typeof(ServerRequestContext), parameters[0].ParameterType);
            Assert.True(typeof(IMessage).IsAssignableFrom(parameters[1].ParameterType));
            Assert.Equal(parameters[1].ParameterType.Name + "Rt", method.ReturnType.Name);
        }
    }

    /// <summary>通过同一处理入口建立测试会话，清除首次地图通知。</summary>
    private void Enter()
    {
        Assert.Null(_cache.Current);
        Assert.True(new LoginHandler().Handle(_cache, new Login { Username = "demo", Password = "demo" }).Success);
        var selected = new SelectRoleHandler().Handle(_cache, new SelectRole { RoleId = _id });
        Assert.True(selected.Success, selected.Error);
        _cache.Notices.Clear();
    }

    /// <summary>只移除本测试创建的独占目录。</summary>
    public void Dispose() => Directory.Delete(_directory, true);
}
