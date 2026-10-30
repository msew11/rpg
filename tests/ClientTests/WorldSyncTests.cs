using Rpg.Const;
using Rpg.ProtocolV2;
using Rpg.Services;
using Rpg.Session.Data;
using Rpg.Tests;
using Rpg.World;
using Godot;

namespace ClientTests;

/// <summary>对应 server-v2-movement：整批原子校验、缺口恢复、公开玩家插值与预测限流。</summary>
[Trait("Runtime", "Godot")]
public sealed class WorldSyncTests : IDisposable
{
    /// <summary>每例独占服务与存档路径。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-world-sync-" + Guid.NewGuid());
    /// <summary>非法远端状态不能提前更新本人；缺口只发一次重同步并接受较新全量。</summary>
    [Fact]
    public void InvalidBatchIsAtomicAndGapUsesOneResync()
    {
        using var services = GameServicesTestFactory.RegisterV2(ConfigTestFixture.CreateEnv(_root, Path.Combine(_root, "server")));
        WorldFramePb? initial = null; EnterWorldRt? entered = null;
        using var frameListener = GameServices.EventBus.Subscribe<WorldUpdateNotice>(notice => initial ??= notice.Frame.Clone());
        using var entryListener = GameServices.EventBus.Subscribe<EnterWorldRt>(reply => entered = reply.Clone());
        var session = GameServices.Session; session.Login("demo1", "123"); Pump(() => !session.AccountRequestPending);
        session.CreateRole("观察者", RoleGender.Male, 1); Pump(() => !session.AccountRequestPending);
        session.SelectRole(session.Roles.Single().RoleId); Pump(() => session.CanMove);
        Assert.NotNull(initial); Assert.Equal(1UL, session.WorldSequence);
        var state = session.Role; var map = session.Map; var failures = new List<Exception>(); GameServices.EventBus.Error += failures.Add;
        var invalid = new WorldFramePb { Sequence = 2, ServerTimeMs = initial!.ServerTimeMs + 1, Self = initial.Self.Clone(), Delta = new() };
        invalid.Self.Position.X += 10;
        var remote = entered!.State.Clone(); remote.RoleId = 999; remote.Position.X = float.NaN;
        invalid.Delta.EnteredRoles.Add(remote); GameServices.EventBus.Publish(new WorldUpdateNotice { Frame = invalid });
        Assert.Single(failures); Assert.Same(state, session.Role); Assert.Same(map, session.Map); Assert.Empty(session.RemoteRoles);
        var gap = invalid.Clone(); gap.Sequence = 3; gap.Delta.EnteredRoles.Clear(); GameServices.EventBus.Publish(new WorldUpdateNotice { Frame = gap });
        Assert.Equal(1UL, session.WorldSequence); Assert.True(session.ResyncMapIfNeeded()); Assert.False(session.ResyncMapIfNeeded());
        var restored = initial.Clone(); restored.Sequence = 4; restored.ServerTimeMs++;
        remote.Position = initial.Self.Position.Clone(); restored.Snapshot.Roles.Add(remote);
        GameServices.EventBus.Publish(new ResyncWorldRt { Frame = restored });
        Assert.Equal(4UL, session.WorldSequence); Assert.Single(session.RemoteRoles); Assert.False(session.ResyncMapIfNeeded());
        GameServices.EventBus.Publish(new WorldUpdateNotice { Frame = initial }); Assert.Equal(4UL, session.WorldSequence);
        Assert.False(session.IsWorldReadOnly);
    }
    /// <summary>对应 server-v2-ground-items 与 server-v2-items：对象及人物同帧原子接纳，位置装备可以一起变化。</summary>
    [Fact]
    public void ObjectDeltaAndPlayerGroupsCommitAtomically()
    {
        using var services = GameServicesTestFactory.RegisterV2(ConfigTestFixture.CreateEnv(_root, Path.Combine(_root, "server")));
        WorldFramePb? initial = null; EnterWorldRt? entered = null;
        using var frameListener = GameServices.EventBus.Subscribe<WorldUpdateNotice>(notice => initial ??= notice.Frame.Clone());
        using var entryListener = GameServices.EventBus.Subscribe<EnterWorldRt>(reply => entered = reply.Clone());
        var session = GameServices.Session; session.Login("demo1", "123"); Pump(() => !session.AccountRequestPending);
        session.CreateRole("对象帧", RoleGender.Male, 1); Pump(() => !session.AccountRequestPending);
        session.SelectRole(session.Roles.Single().RoleId); Pump(() => session.CanMove);
        var remote = entered!.State.Clone(); remote.RoleId = 999;
        var frame = new WorldFramePb { Sequence = 2, ServerTimeMs = initial!.ServerTimeMs + 1, Self = initial.Self.Clone(), Delta = new() { Map = new() { Center = initial.Snapshot.Map.Center.Clone() } } };
        frame.Delta.EnteredRoles.Add(remote);
        var item = new MapObjectPb { ObjectId = Guid.NewGuid().ToString("N"), Revision = 1, X = remote.Position.X, Y = remote.Position.Y, GroundItem = new() { ItemId = 1012, Count = 2 } };
        frame.Delta.Map.UpsertedObjects.Add(item);
        var state = session.Role; var map = session.Map; var errors = new List<Exception>(); GameServices.EventBus.Error += errors.Add;
        GameServices.EventBus.Publish(new WorldUpdateNotice { Frame = frame });
        Assert.Single(errors); Assert.Same(state, session.Role); Assert.Same(map, session.Map); Assert.Empty(session.RemoteRoles);
        item.GroundItem.Count = 1; GameServices.EventBus.Publish(new WorldUpdateNotice { Frame = frame });
        Assert.Single(session.Map!.Objects); Assert.Single(session.RemoteRoles);
        var update = new WorldFramePb { Sequence = 3, ServerTimeMs = frame.ServerTimeMs + 1, Self = frame.Self.Clone(), Delta = new() { Map = new() { Center = frame.Delta.Map.Center.Clone() } } };
        var position = remote.Position.Clone(); position.X += 5;
        update.Delta.MovedRoles.Add(new RolePositionUpdatePb() { RoleId = 999, Position = position });
        update.Delta.EquippedRoles.Add(new RoleEquipmentUpdatePb() { RoleId = 999, Equipment = new() { EquippedItems = { 1012 } } });
        update.Delta.Map.RemovedObjectIds.Add(item.ObjectId);
        GameServices.EventBus.Publish(new WorldUpdateNotice { Frame = update });
        Assert.Empty(session.Map.Objects); Assert.Equal(position.X, session.RemoteRoles[999].Position.Position.X);
        Assert.Single(session.RemoteRoles[999].Equipment.Items);
    }

    /// <summary>100ms插值与有限外推不越过目标，离开删除样本。</summary>
    [Fact]
    public void RemoteBufferInterpolatesAndBoundsExtrapolation()
    {
        var buffer = new RemoteRoleBuffer();
        var role = new DataPublicRole(5, new(5, "其他玩家", 1, RoleGender.Male, 1, 0), new(5, 100, 100, 60, 60, 0),
            new(5, 1, new Vector2(10, 10), 0, 0) { Target = new(30, 10), Speed = 100 }, RoleWorldEquipmentPbData.Empty(5));
        buffer.Accept(1000, new Dictionary<long, DataPublicRole> { [5] = role });
        buffer.Accept(1100, new Dictionary<long, DataPublicRole> { [5] = role with { Position = role.Position with { Position = new(20, 10) } } });
        Assert.Equal(15, Assert.Single(buffer.Render(1050)).Position.X);
        Assert.Equal(30, Assert.Single(buffer.Render(10000)).Position.X);
        buffer.Accept(1200, new Dictionary<long, DataPublicRole>()); Assert.Empty(buffer.Render(1200));
    }
    /// <summary>连续输入立即预测且合并到20Hz；旧确认不能撤销较新的目标。</summary>
    [Fact]
    public void PredictionCoalescesClicksAndReplaysNewerIntentAfterCorrection()
    {
        using var services = GameServicesTestFactory.RegisterV2(ConfigTestFixture.CreateEnv(_root, Path.Combine(_root, "server")));
        var session = GameServices.Session; WorldFramePb? initial = null;
        using var initialListener = GameServices.EventBus.Subscribe<WorldUpdateNotice>(notice => initial ??= notice.Frame.Clone());
        session.Login("demo1", "123"); Pump(() => !session.AccountRequestPending);
        session.CreateRole("预测角色", RoleGender.Male, 1); Pump(() => !session.AccountRequestPending);
        session.SelectRole(session.Roles.Single().RoleId); Pump(() => session.CanMove); GameServices.World.InitializeRole();
        var origin = GameServices.World.RolePosition; var replies = new List<ulong>();
        using var observed = GameServices.EventBus.Subscribe<MoveRt>(reply => replies.Add(reply.ActionId));
        using var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true };
        for (var i = 1; i <= 100; i++)
        { Assert.True(GameServices.World.HandleInput(click, origin + new Vector2(100 + i, 0))); GameServices.World.Tick(.001); GameServices.Net.Tick(0); }
        Assert.True(GameServices.World.RolePosition.X > origin.X);
        GameServices.World.Tick(.05); Pump(() => replies.Contains(100)); Assert.InRange(replies.Count, 1, 4);
        var correction = initial!.Clone(); correction.Sequence = 2; correction.ServerTimeMs++; correction.AcknowledgedAction = 1;
        correction.Self.Position.Target = new() { X = origin.X + 101, Y = origin.Y };
        GameServices.EventBus.Publish(new WorldUpdateNotice { Frame = correction }); GameServices.World.Tick(0);
        Assert.Equal(origin + new Vector2(200, 0), GameServices.World.RoleTarget);
    }
    /// <summary>后台回复由固定帧交付，限定等待避免测试挂起。</summary>
    private static void Pump(Func<bool> done) { var limit = DateTime.UtcNow.AddSeconds(5); while (!done() && DateTime.UtcNow < limit) { GameServices.Net.Tick(0); Thread.Sleep(1); } Assert.True(done()); }
    /// <summary>回收测试目录。</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
