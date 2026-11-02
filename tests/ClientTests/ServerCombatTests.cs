using Rpg.Config;
using Rpg.Const;
using Rpg.Protocol;
using Rpg.Server.ActorProtocol;
using Rpg.Server.Modules;
using Rpg.Server.Modules.Common.Utils;
using Rpg.Server.Modules.Role;
using Rpg.Server.Modules.Role.Async;
using Rpg.Server.Modules.Role.Data;
using Rpg.Server.Modules.Role.Utils;
using Rpg.Server.Modules.World.Data;
using Rpg.Server.Modules.World.Storage;
using Rpg.Server.Runtime;

namespace ClientTests;

/// <summary>对应 openspec/specs/basic-monster-combat：正式World邮箱、可控Role时钟、装备投放与存档恢复。</summary>
[Trait("Runtime", "Godot")]
public sealed class ServerCombatTests : ServerModuleTests
{
    /// <summary>每例独占真实地图存档。</summary>
    private readonly string _root = Path.Combine(Path.GetTempPath(), "rpg-combat-" + Guid.NewGuid());
    /// <summary>构造正式模块及可控角色上下文，共用同一World邮箱。</summary>
    private ServerModules Modules() => new(Config, new DoubleServerStorage(), new DoubleServerStorage(), new WorldStorage(_root));
    /// <summary>只有命中时钟推进才扣血；取消、重复和冷却均不增加伤害。</summary>
    [Fact]
    public async Task WindupCancellationDuplicateAndRecoveryRespectServerClock()
    {
        await using var modules = Modules(); var context = Role(modules); var target = await Spawn(modules);
        var handler = new AttackHandler();
        var first = await handler.HandleAsync(context, new Attack { AttackId = 1, TargetId = target.ObjectId });
        Assert.True(first.Accepted); Assert.Equal(1000, first.Started.DurationMs); Assert.Equal(100, (await Monster(modules)).Monster.Health);
        Assert.False((await handler.HandleAsync(context, new Attack { AttackId = 1, TargetId = target.ObjectId })).Accepted);
        context.Call = new(ServerTimeMs: 1100);
        await new MoveHandler().HandleAsync(context, new Move { ActionId = 2, Target = new() { X = 16000, Y = 16000 } });
        await RoleCombat.AdvanceAsync(context, 1400);
        Assert.Equal(100, (await Monster(modules)).Monster.Health);
        context.Call = new(ServerTimeMs: 1500);
        Assert.False((await handler.HandleAsync(context, new Attack { AttackId = 3, TargetId = target.ObjectId })).Accepted);
        context.Call = new(ServerTimeMs: 2000);
        Assert.True((await handler.HandleAsync(context, new Attack { AttackId = 4, TargetId = target.ObjectId })).Accepted);
        await RoleCombat.AdvanceAsync(context, 2334);
        var health = (await Monster(modules)).Monster.Health; Assert.InRange(health, 97, 99);
        await RoleCombat.AdvanceAsync(context, 2400); Assert.Equal(health, (await Monster(modules)).Monster.Health);
        await RoleCombat.CancelAsync(context);
        context.Call = new(ServerTimeMs: 2500);
        Assert.False((await handler.HandleAsync(context, new Attack { AttackId = 5, TargetId = target.ObjectId })).Accepted);
        await modules.FlushAsync();
        await using var restored = Modules(); Assert.Equal(health, (await Monster(restored)).Monster.Health);
    }
    /// <summary>主手替换范围与攻速，点伤分别累加；仅盾和卸下恢复空手。</summary>
    [Fact]
    public async Task EquipmentRecalculatesDamageRangeAndSpeedAndCancelsWindup()
    {
        await using var modules = Modules(); var context = Role(modules); var target = await Spawn(modules);
        RoleAttributes.Refresh(context); Assert.Equal(48, context.Cache.Attributes[9]);
        foreach (var (id, range, speed, min, max) in new[] { (1012,64d,1.25,5d,10d), (1015,64d,1d,4d,9d), (1017,48d,1.5,4d,8d) })
        {
            var item = new DataItem(1, id, EquipmentSlot.MainHand);
            context.Cache.Commit(new(context.Cache.Role.Value, [item], [new() { InstanceId = 1 }]));
            RoleAttributes.Refresh(context);
            Assert.Equal(range, context.Cache.Attributes[9]); Assert.Equal(speed, context.Cache.Attributes[10]);
            Assert.Equal(min, context.Cache.Attributes[7]); Assert.Equal(max, context.Cache.Attributes[8]);
        }
        context.Cache.Commit(new(context.Cache.Role.Value, [new DataItem(1,1014,EquipmentSlot.OffHand)], [new() { InstanceId = 1 }]));
        RoleAttributes.Refresh(context); Assert.Equal(48, context.Cache.Attributes[9]); Assert.Equal(1, context.Cache.Attributes[10]);
        context.Cache.Commit(new(context.Cache.Role.Value, [new DataItem(1,1012,EquipmentSlot.MainHand)], [new() { InstanceId = 1 }]));
        var start = await new AttackHandler().HandleAsync(context, new Attack { AttackId = 1, TargetId = target.ObjectId });
        Assert.True(start.Accepted); Assert.Equal(800, start.Started.DurationMs);
        await new EquipItemHandler().HandleAsync(context, new EquipItem { InstanceId = 0, Slot = EquipmentSlotPb.MainHand });
        Assert.Null(context.Cache.Attack); Assert.Equal(1800, context.Cache.NextAttackMs); Assert.Equal(48, context.Cache.Attributes[9]);
    }
    /// <summary>多个角色的致死请求在World串行处理，血量不负数且只有一次致死事件。</summary>
    [Fact]
    public async Task ConcurrentLethalHitsRemoveOnceAndStayRemovedAfterRestart()
    {
        await using (var modules = Modules())
        {
            var target = await Spawn(modules);
            var hits = await Task.WhenAll(Enumerable.Range(1, 8).Select(id => modules.World(1).Ask<HitMonsterRt>(new HitMonster {
                RoleId = id, AttackId = 1, TargetId = target.ObjectId, Position = new() { MapId = 1, X = 16000, Y = 16000 },
                Range = 48, DamageMin = 150, DamageMax = 150, ServerTimeMs = 1300 })));
            Assert.Single(hits.Where(hit => hit.Hit));
            var events = await modules.World(1).Ask<DrainCombatEventsRt>(new DrainCombatEvents());
            var killed = Assert.Single(events.Deliveries).Event.Hit; Assert.True(killed.Dead); Assert.Equal(0, killed.Target.Monster.Health); Assert.Equal(150,killed.Damage);
            await modules.FlushAsync();
        }
        await using var restored = Modules();
        Assert.Empty((await restored.World(1).Ask<GetWorldEntrySnapshotRt>(new GetWorldEntrySnapshot { X = 16000, Y = 16000 })).Map.Chunks.SelectMany(chunk => chunk.Objects));
    }
    /// <summary>旧存档缺少血量时按配置补满，标脏保存后可再次恢复。</summary>
    [Fact]
    public async Task LegacyMonsterGetsFullHealthAtLoadAndIsSaved()
    {
        var map = Config.Map.Items[1]; var layout = new DataWorldLayout(1,map.Width,map.Height,map.TileSize,map.ChunkSizeTiles);
        var storage = new WorldStorage(_root);
        await storage.SaveAsync(layout, new([new() { ObjectId = Guid.NewGuid(), Revision = 1, X = 16020, Y = 16000, MonsterTypeId = "1001" }]), [new(15,15)]);
        await using var modules = Modules(); Assert.Equal(100, (await Monster(modules)).Monster.Health);
        await modules.FlushAsync(); Assert.Equal(100, Assert.Single((await storage.LoadAsync(layout)).Objects).Health);
    }
    /// <summary>跨块边界攻击应同时通知能看见攻击者及目标的观察者。</summary>
    [Fact]
    public async Task CombatRecipientsIncludeBothSidesOfChunkBoundary()
    {
        await using var modules = Modules(); var map = Config.Map.Items[1];
        var context = new Rpg.Server.Modules.World.WorldContext(Config, modules,
            new(1,map.Width,map.Height,map.TileSize,map.ChunkSizeTiles),new([]));
        var chunk = map.TileSize * map.ChunkSizeTiles;
        foreach (var (id,x) in new[] { (1,2*chunk-1f),(2,1f),(3,3*chunk+1f) })
        {
            context.Cache.Roles.Membership(id,true);
            context.Cache.Roles.Update(new(id,"观察者",1,1,1,100,100,60,60,[],1,x,1,null,null,180,1,0));
        }
        Rpg.Server.Modules.World.Utils.MonsterCombat.Queue(context,new() { Started=new() {
            MapId=1,RoleId=1,AttackId=1,TargetX=2*chunk+1,TargetY=1 } });
        Assert.Equal(new long[] { 1,2,3 },Assert.Single(context.Cache.CombatEvents).ObserverRoleIds.Order());
    }
    /// <summary>建立等级完整、位于怪物附近的角色缓存。</summary>
    private RoleContext Role(ServerModules modules) => new(Config, modules, 1, new(new() {
        RoleId = 1, MapId = 1, NextInstanceId = 2, Name = "战斗", Level = 1, Health = 100, MaxHealth = 100, Mana = 60, MaxMana = 60,
        Gender = Rpg.Server.Modules.Common.Data.RoleGender.Male, HeadId = 1, X = 16000, Y = 16000 }, [], [])) { Call = new(ServerTimeMs: 1000) };
    /// <summary>使用正式创建处理器生成附近树妖。</summary>
    private static async Task<MapObjectPb> Spawn(ServerModules modules) => (await modules.World(1).Ask<CreateMonsterRt>(new CreateMonster { MonsterTypeId = 1001, X = 16020, Y = 16000 })).Object;
    /// <summary>从正式地图快照检查实际血量。</summary>
    private static async Task<MapObjectPb> Monster(ServerModules modules) => Assert.Single((await modules.World(1).Ask<GetWorldEntrySnapshotRt>(new GetWorldEntrySnapshot { X = 16000, Y = 16000 })).Map.Chunks.SelectMany(chunk => chunk.Objects));
    /// <summary>释放配置并回收本例存档。</summary>
    public override void Dispose() { base.Dispose(); if (Directory.Exists(_root)) Directory.Delete(_root,true); }
}
