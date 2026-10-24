using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Const;
using Rpg.Config;
using Rpg.Services;
using Rpg.Settings;
using Rpg.Net;
using Rpg.Net.Message;
using Rpg.Server;
using Rpg.Session;
using Rpg.World;
using Rpg.World.Actions;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验证 openspec/specs/client-actor-presentation 的全动作、八方向、锚点和播放隔离场景。</summary>
public partial class ActorAnimationSmoke : Node
{
    /// <summary>在 Godot 内载入真实资源并检查玩家世界到动画节点的契约。</summary>
    public override async void _Ready()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "rpg-actor-smoke-" + Guid.NewGuid());
        try
        {
            using var services = GameServicesTestFactory.Register(new EnvironmentService(Path.Combine(temporary, "settings"), temporary));
            var config = GameServices.Config;
            var repo = new AnimationService();
            var net = GameServices.Net;
            LoginRt? loginResponse = null;
            using var login = GameServices.Events.Subscribe<LoginRt>(response =>
            {
                Check(response.Success, response.Error);
                loginResponse = response;
            });
            net.Send(new Login { Username = "demo", Password = "demo" });
            net.Tick(0);
            using var session = new SessionService(net, GameServices.Events, loginResponse!);
            Check(session.LoginCache.Roles.Count == 2, "登录未返回两个演示角色");
            var actor = new ActorView();
            AddChild(actor);
            var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
            var facing = actor.GetNode<Node2D>("Facing");
            var total = 0;
            foreach (var summary in session.LoginCache.Roles)
            {
                session.SelectRole(summary.RoleId);
                net.Tick(0);
                Check(session.RoleCache.IsInitialized, "选角未初始化角色世界状态");
                var role = session.Role;
                var world = new WorldManager();
                world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(config.MapConfig)));
                world.Enqueue(new InitializeRoleAction(role));
                world.Tick(0);
                actor.Present(world.ActorState!);
                Check(body.Frame == 0, "角色切换没有回到首帧");
                foreach (var motion in Enum.GetValues<RoleMotion>())
                {
                    var resource = repo.Get(role.Gender, motion);
                    Check(ReferenceEquals(resource, repo.Get(role.Gender, motion)), "重复加载未复用资源");
                    Check(resource.Frames.GetAnimationNames().Length == 5, "动作缺少五个原始方向");
                    for (var direction = 0; direction < 5; direction++)
                    {
                        var name = $"dir{direction}";
                        var count = resource.Frames.GetFrameCount(name);
                        Check(count > 0, "动画方向没有帧");
                        Check(resource.Frames.GetAnimationLoopMode(name) ==
                            (motion is RoleMotion.Stand or RoleMotion.Run ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None),
                            "动作循环策略错误");
                        for (var frame = 0; frame < count; frame++)
                        {
                            var texture = resource.Frames.GetFrameTexture(name, frame) as AtlasTexture;
                            Check(texture?.Atlas is not null && texture.Region.Size.X > 0, "动画帧纹理加载失败");
                            Check(texture!.Region.End.X <= texture.Atlas.GetWidth() &&
                                texture.Region.End.Y <= texture.Atlas.GetHeight(), "动画帧区域超出图集");
                            total++;
                        }
                    }
                    if (motion == RoleMotion.Run)
                    {
                        world.Enqueue(new MoveAction(1, world.RolePosition + new Vector2(100, 0)));
                        world.Tick(0.1);
                    }
                    else if (motion == RoleMotion.Stand)
                    {
                        world.Enqueue(new MoveAction(1, world.RolePosition));
                        world.Tick(0);
                    }
                    else
                    {
                        world.Enqueue(new SetRoleMotionAction(role.RoleId, motion));
                        world.Tick(0);
                    }
                    var state = world.ActorState!;
                    actor.Present(state);
                    Check(ReferenceEquals(body.SpriteFrames, resource.Frames), "没有使用玩家性别和动作对应的资源");
                    Check(actor.Position == IsometricProjection.ToScreen(state.Position), "角色逻辑位置未投影");
                    Check(body.Position == -resource.Anchor, "动画没有使用原始定位点");
                    var before = state;
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Check(before == world.ActorState, "动画播放反向修改了世界状态");
                }
            }
            Check(total == 605, $"动作资源帧不完整：{total}");

            var run = new DataActorState(1001, RoleGender.Male, Vector2.Zero, RoleMotion.Run, Vector2.Right, 0);
            actor.Present(run);
            body.SetFrameAndProgress(3, 0.4f);
            actor.Present(run with { Position = Vector2.One });
            Check(body.Frame == 3 && Math.Abs(body.FrameProgress - 0.4f) < 0.001f, "位置刷新重启了动画");
            // 屏幕方向与资源编号独立列出，避免测试重复调用映射实现。
            var screenDirections = new[] { Vector2.Down, new Vector2(-1, 1), Vector2.Left,
                new Vector2(-1, -1), Vector2.Up, new Vector2(1, -1), Vector2.Right, new Vector2(1, 1) };
            var expected = new[] { (4, false), (0, false), (1, false), (2, false),
                (3, false), (2, true), (1, true), (0, true) };
            for (var index = 0; index < screenDirections.Length; index++)
            {
                var state = run with { Facing = IsometricProjection.ToLogical(screenDirections[index]) };
                actor.Present(state);
                Check(body.Animation == $"dir{expected[index].Item1}", "八方向资源映射错误");
                Check(facing.Scale.X == (expected[index].Item2 ? -1 : 1), "八方向镜像错误");
                Check(body.Frame == 3 && Math.Abs(body.FrameProgress - 0.4f) < 0.001f, "改向丢失播放进度");
                var anchor = repo.Get(state.Gender, state.Motion).Anchor;
                Check((body.Transform * anchor).IsEqualApprox(Vector2.Zero), "镜像前定位点未对齐节点原点");
                Check((facing.Transform * (body.Transform * anchor)).IsEqualApprox(Vector2.Zero), "镜像后定位点漂移");
            }
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            Check(body.Frame != 3, "跑步帧没有随显示时间推进");
            actor.Present(run with { Motion = RoleMotion.Attack1 });
            body.SpeedScale = 100;
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            Check(!body.IsPlaying() && body.Frame == body.SpriteFrames.GetFrameCount(body.Animation) - 1,
                "非循环动作没有停在末帧");
            actor.Present(run with { Motion = RoleMotion.Attack1, MotionVersion = 1 });
            Check(body.Frame == 0 && body.IsPlaying(), "同一动作的再次请求没有重播");
            actor.Present(run with { Gender = RoleGender.Female });
            Check(body.Frame == 0 && ReferenceEquals(body.SpriteFrames, repo.Get(RoleGender.Female, RoleMotion.Run).Frames),
                "性别更新后残留旧资源或帧");
            if (OS.GetCmdlineUserArgs().Contains("--capture-actors")) await CaptureActors(screenDirections);
            GD.Print("Actor animation smoke passed: 26 motions, 605 frames, 8 directions.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    /// <summary>可选渲染男女八方向示例，供人工核对透明度、方向和身体定位。</summary>
    private async Task CaptureActors(Vector2[] directions)
    {
        var viewport = new SubViewport { Size = new Vector2I(960, 320), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = viewport.Size, Color = new Color("24303d") });
        for (var gender = 0; gender < 2; gender++)
        for (var direction = 0; direction < directions.Length; direction++)
        {
            var position = new Vector2(60 + direction * 120, 130 + gender * 150);
            viewport.AddChild(new ColorRect { Position = position - new Vector2(15, 0), Size = new Vector2(30, 1),
                Color = new Color("75b7cf") });
            var preview = new ActorView();
            viewport.AddChild(preview);
            preview.Present(new DataActorState(1, (RoleGender)gender, IsometricProjection.ToLogical(position),
                RoleMotion.Run, IsometricProjection.ToLogical(directions[direction]), 0));
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = ProjectSettings.GlobalizePath("res://build");
        System.IO.Directory.CreateDirectory(folder);
        var error = viewport.GetTexture().GetImage().SavePng(System.IO.Path.Combine(folder, "actor-preview.png"));
        Check(error == Error.Ok, "角色预览图保存失败");
    }

    /// <summary>以退出码可见的异常报告场景验收失败。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
