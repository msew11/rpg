using Rpg.Const;
using Rpg.Protocol;
using Rpg.Services;
using Rpg.Session.Data;
using Rpg.Tests;

namespace ClientTests;

/// <summary>对应openspec/specs/basic-monster-combat：即时血量、重复命中及旧全量世界帧不能覆盖死亡。</summary>
[Trait("Runtime","Godot")]
public sealed class CombatCacheTests : IDisposable
{
    /// <summary>本例独占服务目录。</summary>
    private readonly string _root=Path.Combine(Path.GetTempPath(),"rpg-combat-cache-"+Guid.NewGuid());
    /// <summary>命中通知不改变原始增量基线，重同步仍保留较新的血量和死亡墓碑。</summary>
    [Fact]
    public void DamageOverlaySurvivesOldFullFramesAndDuplicateHitDoesNotFloatTwice()
    {
        using var services=GameServicesTestFactory.Register(ConfigTestFixture.CreateEnv(_root,Path.Combine(_root,"server")));
        WorldFramePb? initial=null;
        using var listener=GameServices.EventBus.Subscribe<WorldUpdateNotice>(notice=>initial??=notice.Frame.Clone());
        var session=GameServices.Session;session.Login("demo1","123");Pump(()=>!session.AccountRequestPending);
        session.CreateRole("攻击缓存",RoleGender.Male,1);Pump(()=>!session.AccountRequestPending);
        session.SelectRole(session.Roles.Single().RoleId);Pump(()=>session.CanMove);
        var monster=new MapObjectPb { ObjectId=Guid.NewGuid().ToString("N"),Revision=1,X=session.Role.Position.X,Y=session.Role.Position.Y,
            Monster=new() { MonsterTypeId="1001",Health=100,MaxHealth=100 } };
        var frame=initial!.Clone(); frame.Sequence=2;frame.ServerTimeMs++;
        frame.Snapshot.Map.Chunks.Single(chunk=>chunk.Coordinate.Equals(frame.Snapshot.Map.Center)).Objects.Add(monster);
        GameServices.EventBus.Publish(new WorldUpdateNotice { Frame=frame });
        // 远端重新获得控制后输入编号可归一；服务器起手时间仍阻止旧取消覆盖新动作。
        var oldStart = new AttackStartedNotice { MapId=1,RoleId=99,AttackId=10,TargetId=monster.ObjectId,
            StartTimeMs=frame.ServerTimeMs,DurationMs=1000,HitTimeMs=frame.ServerTimeMs+300,Motion=4 };
        GameServices.EventBus.Publish(oldStart);
        var cancel = new AttackCancelledNotice { MapId=1,RoleId=99,AttackId=10,StartTimeMs=oldStart.StartTimeMs };
        GameServices.EventBus.Publish(cancel); Assert.False(session.Attacks.ContainsKey(99));
        GameServices.EventBus.Publish(oldStart); Assert.False(session.Attacks.ContainsKey(99));
        var newStart=oldStart.Clone();newStart.AttackId=1;newStart.StartTimeMs+=2000;newStart.HitTimeMs+=2000;
        GameServices.EventBus.Publish(newStart);GameServices.EventBus.Publish(cancel);GameServices.EventBus.Publish(oldStart);
        Assert.Equal(1ul,session.Attacks[99].AttackId);
        var wounded=monster.Clone();wounded.Revision=2;wounded.Monster.Health=80;
        var hit=new AttackHitNotice { MapId=1,RoleId=session.RoleId,AttackId=1,Target=wounded,Damage=20,ServerTimeMs=frame.ServerTimeMs+1 };
        GameServices.EventBus.Publish(hit);GameServices.EventBus.Publish(hit);
        Assert.Single(session.TakeCombatUpdates().OfType<DataCombatHitUpdate>());Assert.Equal(80,Assert.Single(session.Map!.Objects).Value.Health);
        frame.Sequence=3;frame.ServerTimeMs++;GameServices.EventBus.Publish(new WorldUpdateNotice { Frame=frame });
        Assert.Equal(80,Assert.Single(session.Map!.Objects).Value.Health);
        var dead=wounded.Clone();dead.Revision=3;dead.Monster.Health=0;
        GameServices.EventBus.Publish(new AttackHitNotice { MapId=1,RoleId=session.RoleId,AttackId=2,Target=dead,Damage=80,Dead=true,ServerTimeMs=frame.ServerTimeMs+1 });
        Assert.Empty(session.Map!.Objects);Assert.True(Assert.Single(session.TakeCombatUpdates().OfType<DataCombatHitUpdate>()).Hit.Dead);
        frame.Sequence=4;frame.ServerTimeMs++;GameServices.EventBus.Publish(new ResyncWorldRt { Frame=frame });
        Assert.Empty(session.Map!.Objects);
    }
    /// <summary>在有限时间内推进正式协议和服务器任务。</summary>
    private static void Pump(Func<bool> ready)
    {
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(!ready() && DateTime.UtcNow<deadline) { GameServices.Net.Tick(0.016);Thread.Sleep(1); }
        Assert.True(ready());
    }
    /// <summary>回收本例临时文件。</summary>
    public void Dispose() { if(Directory.Exists(_root)) Directory.Delete(_root,true); }
}
