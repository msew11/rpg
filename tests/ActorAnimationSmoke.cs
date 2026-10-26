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
            RoleTestData.Seed(temporary);
            using var services = GameServicesTestFactory.Register(new EnvService(Path.Combine(temporary, "settings"), temporary));
            var config = GameServices.Config;
            var repo = new AnimationService();
            var net = GameServices.Net;
            LoginRt? loginResponse = null;
            using var login = GameServices.EventBus.Subscribe<LoginRt>(response =>
            {
                Check(response.Success, response.Error);
                loginResponse = response;
            });
            net.Send(new Login { Username = "demo", Password = "demo" });
            net.Tick(0);
            using var session = new SessionService(net, loginResponse!);
            Check(session.LoginCache.Roles.Count == 2, "登录未返回预先准备的两个角色");
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
                world.Enqueue(new InitializeWorldAction(MapUtils.CreateMap(config.Map.Items[1])));
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
            CheckDefaultHeads(repo, actor);
            CheckDefaultWeapon(repo, actor);
            CheckEquipmentVariants(repo, actor);
            CheckKatarHands(repo, actor);
            ActorWeaponHandAssertions.Verify(repo, actor);

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
            CheckWeaponIdentity();
            if (OS.GetCmdlineUserArgs().Contains("--capture-actors"))
            {
                await CaptureActors(screenDirections);
                await CaptureLongWeapons(screenDirections);
                await CaptureKatars(screenDirections);
                await CaptureOtherEquipment(screenDirections);
            }
            GD.Print("Actor animation smoke passed: 26 motions, 605 body frames, 7 equipment types with native katar hands, 8 directions.");
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

    /// <summary>验证 client-actor-presentation 默认头部场景，防止漏导、裁剪偏移和世界锚点回归。</summary>
    private static void CheckDefaultHeads(AnimationService animations, ActorView actor)
    {
        // 取自目视核对后的左下方向头发像素；坐标相对世界锚点，独立于扩大后的画布。
        var samples = new[]
        {
            (RoleGender.Male, RoleMotion.Stand, 0, new Vector2I(-3, -90), "bb8833ff", new Vector2(23, 93)),
            (RoleGender.Male, RoleMotion.Run, 0, new Vector2I(-11, -81), "bb8833ff", new Vector2(45, 100)),
            (RoleGender.Male, RoleMotion.Run, 7, new Vector2I(-10, -85), "bb8833ff", new Vector2(45, 100)),
            (RoleGender.Female, RoleMotion.Stand, 0, new Vector2I(0, -91), "bb7722ff", new Vector2(22, 93)),
            (RoleGender.Female, RoleMotion.Run, 0, new Vector2I(-8, -83), "bb7722ff", new Vector2(40, 94)),
            (RoleGender.Female, RoleMotion.Run, 7, new Vector2I(-9, -85), "bb7722ff", new Vector2(40, 94))
        };
        foreach (var (gender, motion, frame, worldPixel, color, anchor) in samples)
        {
            var resource = animations.Get(gender, motion);
            actor.Present(new DataActorState(1, gender, Vector2.Zero, motion,
                IsometricProjection.ToLogical(new Vector2(-1, 1)), 0, 1));
            actor.GetNode<AnimatedSprite2D>("Facing/Body").SetFrameAndProgress(frame, 0);
            var head = actor.GetNode<Sprite2D>("Facing/Head");
            using var image = head.Texture.GetImage();
            var pixel = worldPixel - (Vector2I)head.Position;
            Check(pixel.X >= 0 && pixel.Y >= 0 && pixel.X < image.GetWidth() && pixel.Y < image.GetHeight(),
                "头部像素超出独立图层");
            Check(image.GetPixelv(pixel).ToHtml() == color, $"默认头部缺失或未随身体挂接：{gender}/{motion}/{frame}");
        }
    }

    /// <summary>role-equipment：全部装备、男女、动作和八方向挂点，空槽清理与替换保留动画进度。</summary>
    private static void CheckEquipmentVariants(AnimationService animations, ActorView actor)
    {
        var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
        foreach (var gender in Enum.GetValues<RoleGender>())
        foreach (var motion in Enum.GetValues<RoleMotion>())
        foreach (var id in Enumerable.Range(1012, 7))
        foreach (var main in id == 1017 ? new[] { true, false } : new[] { id is not (1014 or 1018) })
        for (var direction = 0; direction < 8; direction++)
        {
            var state = new DataActorState(1, gender, Vector2.Zero, motion,
                IsometricProjection.ToLogical(Vector2.FromAngle(direction * Mathf.Pi / 4)), 0, 1, main ? id : 0, main ? 0 : id);
            actor.Present(state);
            var hand = actor.GetNode<Node2D>(main ? "Facing/RightHand" : "Facing/LeftHand");
            var sprite = hand.GetNode<Sprite2D>("Weapon");
            var rear = actor.GetNode<Sprite2D>(main ? "Facing/RightWeaponRear" : "Facing/LeftWeaponRear");
            var resource = animations.Get(gender, motion);
            var (sourceDirection, mirrored) = AnimationService.ResolveDirection(state.Facing);
            var weapon = animations.GetWeapon(id, gender, motion, main != mirrored);
            var count = body.SpriteFrames.GetFrameCount(body.Animation);
            for (var frame = 0; frame < count; frame++)
            {
                body.SetFrameAndProgress(frame, 0.35f);
                var points = main != mirrored ? resource.RightHandAttachments : resource.LeftHandAttachments;
                Check(hand.Position == points[sourceDirection * count + frame] - resource.Anchor, $"装备换手：{id}/{main}/{direction}");
                var textureIndex = Array.IndexOf(weapon.Frames, sprite.Texture);
                Check(sprite.Visible && textureIndex >= 0 && textureIndex / (weapon.Frames.Length / 5) == sourceDirection,
                    $"装备方向或资源错误：{id}/{gender}/{motion}");
                Check(sprite.Position + weapon.Anchors[textureIndex] == Vector2.Zero, "装备连接点错位");
                Check(rear.Visible == (id is 1015 or 1016), "普通装备残留长柄后段或长柄后段缺失");
                if (rear.Visible)
                    Check(ReferenceEquals(rear.Texture, weapon.RearFrames[textureIndex]) &&
                        rear.GlobalPosition == sprite.GlobalPosition &&
                        (rear.GetIndex() < body.GetIndex()) != (hand.GetIndex() < body.GetIndex()),
                        "长柄后段未同步方向、帧或握点");
                actor.Present(state with { MainHandId = 0, OffHandId = 0 });
                Check(!sprite.Visible && sprite.Texture is null && !rear.Visible && rear.Texture is null, "空手残留武器前后段");
                Check(body.Frame == frame && Math.Abs(body.FrameProgress - 0.35f) < 0.001f, "卸下重启动画");
                actor.Present(state);
                Check(body.Frame == frame && sprite.Visible, "重新穿戴重启动画或未显示");
            }
        }
        GD.Print("Equipment actor variants passed: 7 items, both genders, 13 motions, 8 directions, both katar hands and empty slots.");
    }

    /// <summary>client-actor-presentation：独立源哈希校验左手五方向，正背面刀尖向外，镜像侧面不复用错手。</summary>
    private static void CheckKatarHands(AnimationService animations, ActorView actor)
    {
        // 从只读 weaponl5101/5151 原画提取，独立于项目导入资源与运行时手别选择。
        var hashes = new[]
        {
            new[] { "ECD6AB537835815006C46D8C42052E21737A5059414204201A814B3E9C1260FB",
                "8425C44B9ED954E127DA1C6FB5D61B03366E83C05944544FA8D15E3FC0F33313",
                "C5A6C00FB7B0DA123576142A5B11B88BA5C815A7E6FB0AD3AF87440B26B7F861",
                "E09FFB3DC2B9B615DF69DBCFD7EEFD146F546C1EAE1A69481994E45C54D4EF8C",
                "42468B0DB1093F99DBF4A50B6095C80BA2CBF2EBE3631959B832E746B1F3AD93" },
            new[] { "7515CA747B0A755517E9E44F9390D481A4D2A715D144076EE2ED42F079157B44",
                "E1D35855834E68185A93F65844C1CC11E0274BDB1BE927E21E4E8320DEB71055",
                "B5BFED394C0195DD1E43DA2D7F35A9B89245E11FE30AFDC9EC6BFA520873C34D",
                "05112188147D57A20364EADF04E53B3121E0E22C236CF9732F39EAF3CE384577",
                "CB1A95326F9D204B2ABCB1FFA1765D8F497361242985D83DEFABCD07BDDF0792" }
        };
        var directions = new[] { Vector2.Down, new Vector2(-1, 1), Vector2.Left,
            new Vector2(-1, -1), Vector2.Up, new Vector2(1, -1), Vector2.Right, new Vector2(1, 1) };
        foreach (var gender in Enum.GetValues<RoleGender>())
        {
            var left = animations.GetWeapon(1017, gender, RoleMotion.Stand, false);
            var right = animations.GetWeapon(1017, gender, RoleMotion.Stand);
            Check(!ReferenceEquals(left, right), "拳刃左右手仍共用同一原画");
            for (var index = 0; index < 5; index++)
            {
                using var pixels = left.Frames[index].GetImage();
                pixels.Convert(Image.Format.Rgba8);
                var data = pixels.GetData();
                for (var offset = 0; offset < data.Length; offset += 4)
                    if (data[offset + 3] < 255) Array.Clear(data, offset, 3);
                Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)) == hashes[(int)gender][index],
                    $"左手拳刃原始方向像素不符：{gender}/{index}");
            }
            for (var direction = 0; direction < 8; direction++)
            {
                actor.Present(new DataActorState(1, gender, Vector2.Zero, RoleMotion.Stand,
                    IsometricProjection.ToLogical(directions[direction]), 0, 1, 1017, 1017));
                var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                foreach (var main in new[] { true, false })
                {
                    var hand = actor.GetNode<Node2D>(main ? "Facing/RightHand" : "Facing/LeftHand");
                    var sprite = hand.GetNode<Sprite2D>("Weapon");
                    var expected = direction < 5 ? (main ? right : left) : (main ? left : right);
                    Check(expected.Frames.Contains(sprite.Texture), $"镜像方向拳刃使用错手原画：{gender}/{direction}/{main}");
                    if (direction is 0 or 4)
                    {
                        using var pixels = sprite.Texture.GetImage();
                        var bottom = pixels.GetUsedRect().End.Y - 1;
                        var tips = Enumerable.Range(0, pixels.GetWidth()).Where(x => pixels.GetPixel(x, bottom).A > 0).ToArray();
                        Check(tips.Length > 0, "拳刃底部刀尖缺失");
                        var tip = sprite.ToGlobal(new Vector2((float)tips.Average() + 0.5f, bottom + 0.5f));
                        var grip = hand.GlobalPosition;
                        Check(tip.Y > grip.Y && (grip.X < actor.GlobalPosition.X ? tip.X < grip.X : tip.X > grip.X),
                            $"正背面拳刃刀尖未向外展开：{gender}/{direction}/{main}");
                    }
                    else
                    {
                        // 站立左右侧及斜面：原画右手在远侧，镜像后实际远手随之交换。
                        var expectedBehind = direction < 5 ? main : !main;
                        Check((hand.GetIndex() < body.GetIndex()) == expectedBehind,
                            $"侧面拳刃近远手遮挡错误：{gender}/{direction}/{main}");
                    }
                }
            }
        }
        GD.Print("Katar hand identity passed: native left art, both genders, 8 directions, outward front/back tips and side depth.");
    }

    /// <summary>client-actor-presentation：保存双手拳刃男女站立和跑步八方向实绘，核对手部贴合及侧面姿态。</summary>
    private async Task CaptureKatars(Vector2[] directions)
    {
        var viewport = new SubViewport { Size = new Vector2I(1120, 640), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = viewport.Size, Color = new Color("24303d") });
        var actors = new System.Collections.Generic.List<ActorView>();
        for (var row = 0; row < 4; row++)
        for (var direction = 0; direction < 8; direction++)
        {
            var position = new Vector2(70 + direction * 140, 145 + row * 155);
            viewport.AddChild(new Label { Position = position - new Vector2(62, 138),
                Text = $"{(row % 2 == 0 ? "M" : "F")} {(row < 2 ? "stand" : "run")} {direction}" });
            var actor = new ActorView();
            viewport.AddChild(actor);
            actor.Present(new DataActorState(1, (RoleGender)(row % 2), IsometricProjection.ToLogical(position),
                row < 2 ? RoleMotion.Stand : RoleMotion.Run, IsometricProjection.ToLogical(directions[direction]), 0, 1, 1017, 1017));
            actor.GetNode<AnimatedSprite2D>("Facing/Body").Pause();
            actors.Add(actor);
        }
        for (var frame = 0; frame < 8; frame++)
        {
            foreach (var actor in actors)
            {
                var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                body.SetFrameAndProgress(frame % body.SpriteFrames.GetFrameCount(body.Animation), 0);
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var rendered = viewport.GetTexture().GetImage();
            Check(rendered.SavePng(ProjectSettings.GlobalizePath($"res://build/katar-preview-{frame}.png")) == Error.Ok,
                "双手拳刃渲染图保存失败");
        }
        viewport.Free();
        GD.Print("Katar visual captures passed: both genders, stand/run, 8 directions, 8 body frames.");
    }

    /// <summary>client-actor-presentation：补齐大剑、盾牌与飞刀的男女八方向实绘，供源手别与持握复查。</summary>
    private async Task CaptureOtherEquipment(Vector2[] directions)
    {
        foreach (var gender in Enum.GetValues<RoleGender>())
        {
            var viewport = new SubViewport { Size = new Vector2I(1440, 960), Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(viewport);
            viewport.AddChild(new ColorRect { Size = viewport.Size, Color = new Color("24303d") });
            var actors = new System.Collections.Generic.List<ActorView>();
            for (var row = 0; row < 6; row++)
            for (var direction = 0; direction < 8; direction++)
            {
                var id = new[] { 1013, 1014, 1018 }[row / 2];
                var position = new Vector2(160 + direction * 160, 150 + row * 160);
                viewport.AddChild(new Label { Position = position - new Vector2(65, 145),
                    Text = $"{id} {(row % 2 == 0 ? "stand" : "run")} {direction}" });
                var actor = new ActorView();
                viewport.AddChild(actor);
                actor.Present(new DataActorState(1, gender, IsometricProjection.ToLogical(position),
                    row % 2 == 0 ? RoleMotion.Stand : RoleMotion.Run, IsometricProjection.ToLogical(directions[direction]),
                    0, 1, id == 1013 ? id : 0, id == 1013 ? 0 : id));
                actor.GetNode<AnimatedSprite2D>("Facing/Body").Pause();
                actors.Add(actor);
            }
            for (var frame = 0; frame < 8; frame++)
            {
                foreach (var actor in actors)
                {
                    var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                    body.SetFrameAndProgress(frame % body.SpriteFrames.GetFrameCount(body.Animation), 0);
                }
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var rendered = viewport.GetTexture().GetImage();
                Check(rendered.SavePng(ProjectSettings.GlobalizePath($"res://build/equipment-audit-{gender}-{frame}.png")) == Error.Ok,
                    "装备复查渲染图保存失败");
            }
            viewport.Free();
        }
        GD.Print("Other equipment captures passed: greatsword/shield/throwing blade, both genders, stand/run, 8 directions and frames.");
    }

    /// <summary>构造明确装备长剑的外观快照，空手默认不再显示武器。</summary>
    private static DataActorState SwordState(long role, RoleGender gender, Vector2 position, RoleMotion motion,
        Vector2 facing, long version, int headId = 0) => new(role, gender, position, motion, facing, version, headId, 1012);

    /// <summary>保留长剑的黄金挂点与内部遮挡验收，角色状态显式带装备。</summary>
    private static void CheckDefaultWeapon(AnimationService animations, ActorView actor)
    {
        var directions = new[] { Vector2.Down, new Vector2(-1, 1), Vector2.Left,
            new Vector2(-1, -1), Vector2.Up, new Vector2(1, -1), Vector2.Right, new Vector2(1, 1) };
        var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
        var weaponNode = actor.GetNode<Sprite2D>("Facing/RightHand/Weapon");
        var handNode = actor.GetNode<Node2D>("Facing/RightHand");
        var head = actor.GetNode<Sprite2D>("Facing/Head");
        foreach (var gender in Enum.GetValues<RoleGender>())
        foreach (var motion in Enum.GetValues<RoleMotion>())
        {
            var resource = animations.Get(gender, motion);
            var weapon = animations.GetWeapon(1012, gender, motion);
            Check(ReferenceEquals(weapon, animations.GetWeapon(1012, gender, motion)), "武器没有复用缓存");
            Check(resource.RightHandAttachments.Length == resource.HeadAttachments.Length &&
                resource.LeftHandAttachments.Length == resource.HeadAttachments.Length, "身体缺少逐帧双手连接点");
            for (var direction = 0; direction < directions.Length; direction++)
            {
                var state = SwordState(1, gender, Vector2.Zero, motion,
                    IsometricProjection.ToLogical(directions[direction]), 0);
                actor.Present(state);
                var count = body.SpriteFrames.GetFrameCount(body.Animation);
                for (var frame = 0; frame < count; frame++)
                {
                    body.SetFrameAndProgress(frame, 0.4f);
                    Check(weapon.Frames.Contains(weaponNode.Texture), "武器残留其他角色或动作纹理");
                    using var image = weaponNode.Texture.GetImage();
                    Check(image.GetUsedRect().HasArea(), "武器帧像素为空");
                    var source = Array.IndexOf(weapon.Frames, weaponNode.Texture);
                    Check(source / (weapon.Frames.Length / 5) == int.Parse(body.Animation.ToString()[3..]),
                        "武器和身体朝向不同步");
                    Check(weaponNode.ZIndex == 0 && handNode.ZIndex == 0, "武器层级影响其他实体坐标遮挡");
                    if (direction == 0) Check(handNode.GetIndex() > body.GetIndex(), "正面剑身没有遮挡身体");
                    if (direction == 4) Check(handNode.GetIndex() < body.GetIndex(), "背面身体没有遮挡剑身");
                    var mirrored = direction >= 5;
                    var points = mirrored ? resource.LeftHandAttachments : resource.RightHandAttachments;
                    Check(handNode.Position == points[int.Parse(body.Animation.ToString()[3..]) * count + frame] - resource.Anchor,
                        "右手挂点未随身体帧更新");
                    Check(weaponNode.Position + weapon.Anchors[source] == Vector2.Zero, "剑柄没有对齐右手挂点原点");
                    actor.Present(state with { Position = Vector2.One });
                    Check(body.Frame == frame && Math.Abs(body.FrameProgress - 0.4f) < 0.001f,
                        "武器更新重启身体时钟");
                    Check(head.Texture is not null && weaponNode.Visible, "组合图层丢失");
                }
            }
        }
        // 从源画布和正背面身体辨认实际右手，独立记录八方向世界坐标，避免重复错误的挂接公式。
        var standingHands = new[]
        {
            new[] { new Vector2(-18, -42), new Vector2(-10, -48), new Vector2(4, -49), new Vector2(15, -45),
                new Vector2(17, -38), new Vector2(14, -31), new Vector2(-1, -28), new Vector2(-14, -32) },
            new[] { new Vector2(-15, -43), new Vector2(-8, -48), new Vector2(5, -49), new Vector2(15, -45),
                new Vector2(16, -39), new Vector2(12, -34), new Vector2(0, -31), new Vector2(-13, -34) }
        };
        for (var gender = 0; gender < 2; gender++)
        for (var direction = 0; direction < directions.Length; direction++)
        {
            actor.Present(SwordState(1, (RoleGender)gender, Vector2.Zero, RoleMotion.Stand,
                IsometricProjection.ToLogical(directions[direction]), 0));
            Check(handNode.GlobalPosition == standingHands[gender][direction], $"八方向换到左手：{gender}/{direction}");
        }
        // 跑步摆臂的黄金挂点和前后层次，防止只修复站立首帧。
        var samples = new[]
        {
            (RoleGender.Male, RoleMotion.Stand, new Vector2(-1, 1), 0, new Vector2(-10, -48), true),
            (RoleGender.Male, RoleMotion.Run, new Vector2(-1, 1), 0, new Vector2(-22, -41), false),
            (RoleGender.Male, RoleMotion.Run, new Vector2(-1, 1), 3, new Vector2(-27, -41), true),
            (RoleGender.Female, RoleMotion.Stand, Vector2.Down, 0, new Vector2(-15, -43), false),
            (RoleGender.Female, RoleMotion.Run, new Vector2(-1, 1), 3, new Vector2(-5, -62), true)
        };
        foreach (var (gender, motion, screenDirection, frame, position, behind) in samples)
        {
            actor.Present(SwordState(1, gender, Vector2.Zero, motion,
                IsometricProjection.ToLogical(screenDirection), 0));
            body.SetFrameAndProgress(frame, 0);
            Check(handNode.Position == position, $"右手挂点坐标漂移：{gender}/{motion}/{frame}");
            Check((handNode.GetIndex() < body.GetIndex()) == behind, "右手逐帧遮挡回归");
        }
        var run = SwordState(1, RoleGender.Male, Vector2.Zero, RoleMotion.Run,
            IsometricProjection.ToLogical(new Vector2(-1, 1)), 0);
        actor.Present(run);
        body.SetFrameAndProgress(3, 0.4f);
        actor.Present(run with { Facing = IsometricProjection.ToLogical(new Vector2(1, 1)) });
        Check(body.Frame == 3 && handNode.GlobalPosition == new Vector2(-15, -32) &&
            actor.GetNode<Node2D>("Facing").Scale.X == -1, "镜像改向丢失武器帧或握点");
        actor.Present(run with { Motion = RoleMotion.Stand });
        Check(body.Frame == 0 && handNode.GlobalPosition == new Vector2(-10, -48), "停止未恢复右手站立持握");
    }

    /// <summary>可选渲染男女八方向示例，供人工核对透明度、方向和身体定位。</summary>
    private async Task CaptureActors(Vector2[] directions)
    {
        var viewport = new SubViewport { Size = new Vector2I(960, 640), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = viewport.Size, Color = new Color("24303d") });
        var runners = new System.Collections.Generic.List<AnimatedSprite2D>();
        var actors = new System.Collections.Generic.List<ActorView>();
        var overlapCounts = new int[8];
        for (var row = 0; row < 4; row++)
        for (var direction = 0; direction < directions.Length; direction++)
        {
            var position = new Vector2(60 + direction * 120, 135 + row * 155);
            viewport.AddChild(new ColorRect { Position = position - new Vector2(15, 0), Size = new Vector2(30, 1),
                Color = new Color("75b7cf") });
            var preview = new ActorView();
            viewport.AddChild(preview);
            actors.Add(preview);
            preview.Present(SwordState(1, (RoleGender)(row % 2), IsometricProjection.ToLogical(position),
                row < 2 ? RoleMotion.Stand : RoleMotion.Run, IsometricProjection.ToLogical(directions[direction]), 0));
            var sprite = preview.GetNode<AnimatedSprite2D>("Facing/Body");
            sprite.Pause();
            if (row >= 2) runners.Add(sprite);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var folder = ProjectSettings.GlobalizePath("res://build");
        System.IO.Directory.CreateDirectory(folder);
        for (var frame = 0; frame < 8; frame++)
        {
            foreach (var runner in runners) runner.SetFrameAndProgress(frame, 0);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var rendered = viewport.GetTexture().GetImage();
            for (var row = 0; row < 4; row++)
            for (var view = 0; view < 2; view++)
                overlapCounts[row * 2 + view] += CheckWeaponOverlap(rendered, actors[row * 8 + view * 4], view == 0);
            var error = rendered.SavePng(System.IO.Path.Combine(folder, $"weapon-preview-{frame}.png"));
            Check(error == Error.Ok, "持剑预览图保存失败");
        }
        Check(overlapCounts.All(count => count > 0), $"正背面没有实际交叠像素：{string.Join(',', overlapCounts)}");
        GD.Print($"Weapon rendered occlusion pixels passed: {string.Join(',', overlapCounts)}.");
        viewport.Free();
    }

    /// <summary>client-actor-presentation：以七件装备的原始首帧像素哈希阻止再次绑定错误系列。</summary>
    private static void CheckWeaponIdentity()
    {
        foreach (var (id, expected) in new[]
        {
            (1012, "666BDB84AFBF133186BC6941915B7CD0289894A27F88C089C945F3D0FD0ED1B6"),
            (1013, "1B74063A55FBE80D892DFCF96E1604DFA8EE80A563F346292854D71025F65F31"),
            (1014, "926CE692A5E6387CB849E7DEC9D3D8C8EE9311EAFED1728F1B465CDA7817BAB0"),
            (1015, "A1A2C0F1267ED809EA9B65A41CA3C90DDD73375E1B1A0E1B129B2E998425FC8D"),
            (1016, "DD7DDDC8EF4DF8C28E8255A18F9B623815519F3EA352E0AB77CA18558956CF3D"),
            (1017, "5F124D27C4B959A8C1AD997478849B87DA9E3C4F342DD4099A35B18FE603C91B"),
            (1018, "55889D97FD2F69B24FF0E13473BA0A74B5BD77E5DF2F91EBABA3CED8D97310FB")
        })
        {
            using var pixels = GameServices.Animation.GetWeapon(id, RoleGender.Male, RoleMotion.Stand, id != 1018).Frames[0].GetImage();
            pixels.Convert(Image.Format.Rgba8);
            var data = pixels.GetData();
            // Godot 导入会填充半透明边缘的 RGB，保留全部 alpha 及不透明颜色进行比较。
            for (var offset = 0; offset < data.Length; offset += 4)
                if (data[offset + 3] < 255) Array.Clear(data, offset, 3);
            Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)) == expected,
                $"装备外观与原始图标对应系列不符：{id}/{pixels.GetSize()}/{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data))}");
        }
    }

    /// <summary>client-actor-presentation：渲染男女枪杖的站立、跑步和八方向，核验后段交叠像素及握点同步。</summary>
    private async Task CaptureLongWeapons(Vector2[] directions)
    {
        var viewport = new SubViewport { Size = new Vector2I(960, 1200), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = viewport.Size, Color = new Color("24303d") });
        var actors = new System.Collections.Generic.List<ActorView>();
        var totals = new int[16];
        for (var row = 0; row < 8; row++)
        for (var direction = 0; direction < 8; direction++)
        {
            var actor = new ActorView();
            viewport.AddChild(actor);
            var position = new Vector2(60 + direction * 120, 135 + row * 150);
            var id = row % 4 < 2 ? 1015 : 1016;
            var motion = row % 2 == 0 ? RoleMotion.Stand : RoleMotion.Run;
            actor.Present(new DataActorState(1, (RoleGender)(row / 4), IsometricProjection.ToLogical(position),
                motion, IsometricProjection.ToLogical(directions[direction]), 0, 1, id));
            actor.GetNode<AnimatedSprite2D>("Facing/Body").Pause();
            actors.Add(actor);
        }
        for (var frame = 0; frame < 8; frame++)
        {
            foreach (var actor in actors)
            {
                var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                body.SetFrameAndProgress(frame % body.SpriteFrames.GetFrameCount(body.Animation), 0);
                var rear = actor.GetNode<Sprite2D>("Facing/RightWeaponRear");
                var front = actor.GetNode<Sprite2D>("Facing/RightHand/Weapon");
                Check(rear.Visible && rear.Texture is not null && rear.GlobalPosition == front.GlobalPosition &&
                    (rear.GetIndex() < body.GetIndex()) != (front.GetParent().GetIndex() < body.GetIndex()) &&
                    rear.ZIndex == 0, "长柄分层未随握点和身体帧同步或前后段未分处身体两侧");
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var rendered = viewport.GetTexture().GetImage();
            for (var index = 0; index < actors.Count; index++)
            {
                var actor = actors[index];
                var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                // 正背面预期独立于后段实现；其他方向须与已有前段的身体层次相反。
                var inFront = (index % 8) switch
                {
                    0 => false,
                    4 => true,
                    _ => actor.GetNode<Node2D>("Facing/RightHand").GetIndex() < body.GetIndex()
                };
                totals[index / 8 * 2 + (inFront ? 1 : 0)] += CheckRearOverlap(rendered, actor, inFront);
            }
            if (frame == 0) rendered.SavePng(ProjectSettings.GlobalizePath("res://build/long-weapon-preview.png"));
        }
        Check(totals.All(total => total > 0), $"男女站立或跑步缺少枪杖后段交叠验收：{string.Join(',', totals)}");
        GD.Print($"Long weapon rendered occlusion pixels passed: {string.Join(',', totals)}.");
        viewport.Free();
    }

    /// <summary>跳过头部覆盖区域，核对正面交叠来自身体、背面交叠来自后段。</summary>
    private static int CheckRearOverlap(Image rendered, ActorView actor, bool inFront)
    {
        var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
        var rear = actor.GetNode<Sprite2D>("Facing/RightWeaponRear");
        var head = actor.GetNode<Sprite2D>("Facing/Head");
        using var bodyImage = body.SpriteFrames.GetFrameTexture(body.Animation, body.Frame).GetImage();
        using var rearImage = rear.Texture.GetImage();
        using var headImage = head.Texture.GetImage();
        var count = 0;
        for (var y = 0; y < rearImage.GetHeight(); y++)
        for (var x = 0; x < rearImage.GetWidth(); x++)
        {
            if (rearImage.GetPixel(x, y).A < 1) continue;
            var world = rear.ToGlobal(new Vector2(x + 0.5f, y + 0.5f));
            var point = (Vector2I)body.ToLocal(world).Floor();
            if (!bodyImage.GetUsedRect().HasPoint(point) || bodyImage.GetPixelv(point).A < 1) continue;
            var headPoint = (Vector2I)head.ToLocal(world).Floor();
            if (headImage.GetUsedRect().HasPoint(headPoint) && headImage.GetPixelv(headPoint).A > 0) continue;
            var actual = rendered.GetPixelv((Vector2I)world.Floor());
            var expected = inFront ? rearImage.GetPixel(x, y) : bodyImage.GetPixelv(point);
            Check(Mathf.Abs(actual.R - expected.R) < 0.02f && Mathf.Abs(actual.G - expected.G) < 0.02f &&
                Mathf.Abs(actual.B - expected.B) < 0.02f, $"长柄后段遮挡关系错误：前置={inFront}/{world}");
            count++;
        }
        return count;
    }

    /// <summary>验证 client-actor-presentation 正背面遮挡：直接比较 GPU 渲染的交叠像素与应在前的原图颜色。</summary>
    private static int CheckWeaponOverlap(Image rendered, ActorView actor, bool front)
    {
        var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
        var weapon = actor.GetNode<Sprite2D>("Facing/RightHand/Weapon");
        var head = actor.GetNode<Sprite2D>("Facing/Head");
        using var bodyImage = body.SpriteFrames.GetFrameTexture(body.Animation, body.Frame).GetImage();
        using var weaponImage = weapon.Texture.GetImage();
        using var headImage = head.Texture.GetImage();
        var checkedPixels = 0;
        for (var y = 0; y < weaponImage.GetHeight(); y++)
        for (var x = 0; x < weaponImage.GetWidth(); x++)
        {
            var weaponColor = weaponImage.GetPixel(x, y);
            if (weaponColor.A < 1) continue;
            var world = weapon.ToGlobal(new Vector2(x + 0.5f, y + 0.5f));
            var bodyPoint = (Vector2I)body.ToLocal(world).Floor();
            if (!bodyImage.GetUsedRect().HasPoint(bodyPoint)) continue;
            var bodyColor = bodyImage.GetPixelv(bodyPoint);
            if (bodyColor.A < 1 || bodyColor.IsEqualApprox(weaponColor)) continue;
            var headPoint = (Vector2I)head.ToLocal(world).Floor();
            if (headImage.GetUsedRect().HasPoint(headPoint) && headImage.GetPixelv(headPoint).A > 0) continue;
            var pixel = rendered.GetPixelv((Vector2I)world.Floor());
            var expected = front ? weaponColor : bodyColor;
            Check(Mathf.Abs(pixel.R - expected.R) < 0.02f && Mathf.Abs(pixel.G - expected.G) < 0.02f &&
                Mathf.Abs(pixel.B - expected.B) < 0.02f, $"实际渲染遮挡错误：{(front ? "正面" : "背面")}/{body.Frame}/{world}");
            checkedPixels++;
        }
        return checkedPixels;
    }

    /// <summary>以退出码可见的异常报告场景验收失败。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
