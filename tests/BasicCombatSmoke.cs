using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Godot;
using Rpg.Const;
using Rpg.Protocol;
using Rpg.Services;
using Rpg.Server.Modules.Role.Data;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>对应 openspec/specs/basic-monster-combat：真实左键走近、男女空手及全部武器、连续攻击、取消、缩放与致死跳字。</summary>
public partial class BasicCombatSmoke : Node
{
    /// <summary>在隔离配置和正式服务器中逐一验证普攻闭环。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-basic-combat-" + Guid.NewGuid());
        DoubleMainController? main = null; var subscriptions = new List<IDisposable>();
        try
        {
            var server = Path.Combine(directory,"server"); RoleTestData.Seed(server);
            var ids = new[] { 1014,1012,1013,1015,1016,1017,1018 };
            foreach (var roleId in new long[] { 1001,1002 })
                RoleTestData.SetItems(server,roleId,ids.Select((id,index) => new DataItem(id,id,index,1)),2000);
            var config = Path.Combine(directory,"config"); Directory.CreateDirectory(config);
            foreach (var file in Directory.GetFiles(ProjectSettings.GlobalizePath("res://config"),"*.json")) File.Copy(file,Path.Combine(config,Path.GetFileName(file)));
            var fileName = Path.Combine(config,"monster.json"); var rows = JsonNode.Parse(File.ReadAllText(fileName))!.AsArray();
            rows[0]!["attributes"]!["2"] = 9; File.WriteAllText(fileName,rows.ToJsonString());
            main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = server, ConfigDirectory = config, EnvFilePath = Path.Combine(directory,".env") };
            AddChild(main); await Frames();
            var errors = new List<ErrorRt>(); var hits = new List<AttackHitNotice>(); var starts = new List<AttackStartedNotice>();
            subscriptions.Add(GameServices.EventBus.Subscribe<ErrorRt>(errors.Add));
            subscriptions.Add(GameServices.EventBus.Subscribe<AttackHitNotice>(notice => hits.Add(notice.Clone())));
            subscriptions.Add(GameServices.EventBus.Subscribe<AttackStartedNotice>(notice => starts.Add(notice.Clone())));
            main.FindChildren("*","Button",true,false).OfType<Button>().Single(button => button.Text == "登录").EmitSignal(Button.SignalName.Pressed);
            await Frames();
            foreach (var roleId in new long[] { 1001,1002 })
            {
                ((Button)main.FindChild("SelectRole"+roleId,true,false)).EmitSignal(Button.SignalName.Pressed);
                ((Button)main.FindChild("EnterRole",true,false)).EmitSignal(Button.SignalName.Pressed);
                await Frames(); await Until(() => GameServices.Session.CanMove);
                var monsters = (MonstersView)main.FindChild("MonstersView",true,false);
                foreach (var itemId in new[] { 0 }.Concat(ids))
                {
                    if (itemId != 0)
                    {
                        GameServices.Session.EquipItem(itemId,itemId == 1014 ? EquipmentSlot.OffHand : EquipmentSlot.MainHand);
                        await Until(() => GameServices.Session.Equipment.Items.Any(item => item.ItemId == itemId));
                        await Frames();
                    }
                    var origin = GameServices.World.RolePosition; var position = origin + new Vector2(240,0);
                    var before = GameServices.World.MapObjects!.Objects.Keys.ToHashSet();
                    GameServices.Session.SendGm(FormattableString.Invariant($"monster 1001 {position.X:R} {position.Y:R}"));
                    await Until(() => GameServices.World.MapObjects!.Objects.Keys.Except(before).Any());
                    var id = GameServices.World.MapObjects!.Objects.Keys.Except(before).Single();
                    await Until(() => monsters.GetNodeOrNull<Sprite2D>("Monster_"+id.ToString("N")) is not null);
                    var sprite = monsters.GetNode<Sprite2D>("Monster_"+id.ToString("N"));
                    // 真实像素命中经过相机变换；UI遮挡不能启动攻击。
                    var point = Pixel(sprite);
                    if (itemId == 0)
                    {
                        var layer = new CanvasLayer { Layer = 30 }; var panel = new Panel { Position = point-new Vector2(20,20), Size = new Vector2(40,40) };
                        layer.AddChild(panel); main.AddChild(layer); await Frames(); Click(point,MouseButton.Left); await Frames();
                        Check(GameServices.World.AttackTarget is null,"UI左键穿透启动攻击"); layer.QueueFree(); await Frames();
                    }
                    if (itemId == 0)
                    {
                        Click(Pixel(sprite),MouseButton.Left); await Frames(); Click(new Vector2(550,160),MouseButton.Right); await Frames();
                        Check(GameServices.World.AttackTarget is null && GameServices.World.RoleTarget is null,"取消走近后仍然追向旧目标");
                    }
                    var beforeSelect = GameServices.World.RolePosition;
                    Click(Pixel(sprite),MouseButton.Right); await Frames();
                    Check(monsters.SelectedObjectId == id && GameServices.World.AttackTarget is null &&
                        GameServices.World.RolePosition == beforeSelect, "右键选怪意外启动攻击或走近");
                    var firstHit = hits.Count; Click(Pixel(sprite),MouseButton.Left);
                    Check(monsters.SelectedObjectId == id && GameServices.World.AttackTarget == id,"左键没有同时选怪和提交攻击目标");
                    await Until(() => starts.Any(start => start.TargetId == id.ToString("N")));
                    var started = starts.Last(start => start.TargetId == id.ToString("N"));
                    var values = GameServices.Session.Role.Attributes.Values;
                    Check(Math.Abs(started.DurationMs-1000/values[10])<0.001,"攻速未决定完整动作时长");
                    Check(GameServices.World.RolePosition.DistanceTo(position)<=values[9]+0.1,"范围外播放攻击");
                    if (itemId == 1015) Check(values[9]==64 && started.Motion==(int)RoleMotion.Attack2,"法杖不是近战攻击2");

                    Check(GameServices.World.ActorState!.AttackDurationMs>0,"没有预测攻击动作");
                    var timing = GameServices.Config.Basic.Value.AttackTiming(GameServices.Session.Role.Gender,(RoleMotion)started.Motion);
                    Check(Math.Abs(started.HitTimeMs-started.StartTimeMs-started.DurationMs*timing.HitRatio)<0.001,"命中点未按完整攻击间隔缩放");
                    var actor = (ActorView)main.FindChild("ActorView",true,false);
                    var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                    var sourceMs = body.SpriteFrames.GetFrameCount(body.Animation)/body.SpriteFrames.GetAnimationSpeed(body.Animation)*1000;
                    Check(Math.Abs(sourceMs/body.SpeedScale-started.DurationMs)<0.01,"真实身体动画未同比缩放");
                    await Until(() => hits.Skip(firstHit).Any(hit => hit.Target.ObjectId == id.ToString("N")));
                    var hit = hits.Last(value => value.Target.ObjectId == id.ToString("N"));
                    Check(hit.Damage>=values[7] && hit.Damage<=values[8],"伤害不在最终闭区间");
                    await Until(() => main.FindChildren("*","Node",true,false).OfType<DamageNumberView>().Any());
                    if (itemId == 1015 && roleId == 1001 && OS.GetCmdlineUserArgs().Contains("--capture"))
                    {
                        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                        using var capture=GetViewport().GetTexture().GetImage(); capture.SavePng(ProjectSettings.GlobalizePath("res://build/basic-combat.png"));
                    }
                    if (itemId == 0)
                    {
                        await Until(() => hits.Skip(firstHit).Count(value => value.Target.ObjectId == id.ToString("N"))>=2);
                        var count = hits.Count; Click(new Vector2(550,160),MouseButton.Right); await Frames();
                        Check(GameServices.World.AttackTarget is null,"右键空地没有取消自动攻击");
                        await ToSignal(GetTree().CreateTimer(0.5),SceneTreeTimer.SignalName.Timeout);
                        Check(hits.Count==count,"取消前摇后仍然扣血");
                    }
                    else { Click(new Vector2(550,160),MouseButton.Right); await Frames(); }
                }
                // 最后一把飞刀持续攻击直到死亡，最后一次伤害在怪物移除后仍显示。
                var last = hits.Last().Target; if (!hits.Last().Dead)
                {
                    var sprite = monsters.GetNodeOrNull<Sprite2D>("Monster_"+last.ObjectId);
                    if (sprite is not null) Click(Pixel(sprite),MouseButton.Left);
                    await Until(() => hits.Any(hit => hit.Target.ObjectId==last.ObjectId && hit.Dead));
                }
                Check(!GameServices.Session.Map!.Objects.ContainsKey(Guid.ParseExact(last.ObjectId,"N")),"致死后怪物仍在可见缓存");
                Check(main.FindChildren("DamageNumber","Node",true,false).OfType<DamageNumberView>().Any(),"致死移除吞掉最后跳字");
                main.FindChildren("*","Button",true,false).OfType<Button>().Single(button => button.Text=="返回角色选择").EmitSignal(Button.SignalName.Pressed);
                await Frames();
            }
            Check(errors.Count==0,"普攻验收出现服务器错误");
            GD.Print("Basic combat smoke passed: male/female unarmed, shield, all six weapons, real left-click approach, automatic repeats, cancellation, melee staff, speed scaling, damage and lethal floating text.");
            main.QueueFree(); await Frames(); main=null; GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
        finally
        {
            foreach (var subscription in subscriptions) subscription.Dispose();
            if (main is not null && IsInstanceValid(main)) main.Free();
            if (Directory.Exists(directory)) Directory.Delete(directory,true);
        }
    }
    /// <summary>查找贴图可见像素并转换为真实视口点击坐标。</summary>
    private static Vector2 Pixel(Sprite2D sprite)
    {
        using var image = (Image)sprite.Texture.GetImage().Duplicate();
        for (var y=30;y<image.GetHeight();y++) for(var x=20;x<image.GetWidth();x++)
            if(image.GetPixel(x,y).A>0.8) return sprite.GetGlobalTransformWithCanvas()*(sprite.GetRect().Position+new Vector2(x+0.5f,y+0.5f));
        throw new InvalidOperationException("未找到怪物可见像素");
    }
    /// <summary>经视口投递完整鼠标输入，不直接调用业务回调。</summary>
    private void Click(Vector2 point,MouseButton button)
    {
        using var motion=new InputEventMouseMotion { Position=point,GlobalPosition=point }; GetViewport().PushInput(motion,true);
        using var press=new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=button,Pressed=true };
        using var release=new InputEventMouseButton { Position=point,GlobalPosition=point,ButtonIndex=button };
        GetViewport().PushInput(press,true);GetViewport().PushInput(release,true);
    }
    /// <summary>等待真实网络、物理和UI推进，超时报告具体阶段。</summary>
    private async Task Until(Func<bool> condition)
    {
        var deadline=DateTime.UtcNow.AddSeconds(12);
        while(!condition() && DateTime.UtcNow<deadline) await Frames();
        Check(condition(),"普攻场景等待超时");
    }
    /// <summary>账号请求完成后推进少量真实物理帧。</summary>
    private async Task Frames()
    {
        await SmokeNetwork.WaitForAccount(this);
        for(var index=0;index<3;index++) await ToSignal(GetTree(),SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    }
    /// <summary>断言失败通过场景非零退出报告。</summary>
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}
