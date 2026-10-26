using System;
using System.Linq;
using System.Text.Json;
using Godot;
using Rpg.Const;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>对应client-actor-presentation来源合成场景，独立核验动作类别、镜像、挂点及完整部件排序。</summary>
internal static class ActorSourceCompositionAssertions
{
    /// <summary>遍历当前全部允许组合的男女13请求、八方向及身体帧，读取独立来源黄金层次。</summary>
    public static void Verify(AnimationService animations, ActorView actor)
    {
        using var golden = JsonDocument.Parse(FileAccess.GetFileAsString("res://tests/fixtures/actor_weapon_sources.json"));
        var layers = golden.RootElement.GetProperty("layers");
        var directions = new[] { Vector2.Down, new Vector2(-1, 1), Vector2.Left, new Vector2(-1, -1),
            Vector2.Up, new Vector2(1, -1), Vector2.Right, new Vector2(1, 1) };
        var nativeDirections = new[] { 4, 0, 1, 2, 3, 2, 1, 0 };
        var partNames = new[] { "Body", "Head", "LeftHand", "RightHand" };
        var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
        var total = 0;
        foreach (var (main, off) in new[] { (0, 0), (0, 1014), (1012, 0), (1012, 1014),
                     (1013, 0), (1015, 0), (1016, 0), (1017, 0), (1018, 0) })
        foreach (var gender in Enum.GetValues<RoleGender>())
        foreach (var requested in Enum.GetValues<RoleMotion>())
        for (var direction = 0; direction < 8; direction++)
        {
            var family = (main != 0 ? main : off) switch { 1012 or 1013 => 1, 1015 or 1016 => 2, 1017 or 1018 => 3, _ => 0 };
            var resolved = requested;
            if (family != 0 && requested is RoleMotion.Attack1 or RoleMotion.Attack2 or RoleMotion.Attack3)
                resolved = RoleMotion.Attack1 + family - 1;
            if (family != 0 && requested is RoleMotion.Prepare1 or RoleMotion.Prepare2 or RoleMotion.Prepare3)
                resolved = RoleMotion.Prepare1 + family - 1;
            var state = new DataActorState(1, gender, Vector2.Zero, requested,
                IsometricProjection.ToLogical(directions[direction]), 0, 1, main, off);
            actor.Present(state);
            var resource = animations.Get(gender, resolved);
            Require(ReferenceEquals(body.SpriteFrames, resource.Frames), $"原生动作类别不符：{main}/{off}/{requested}");
            body.Pause();
            var count = body.SpriteFrames.GetFrameCount(body.Animation);
            var sourceDirection = nativeDirections[direction];
            for (var frame = 0; frame < count; frame++)
            {
                body.SetFrameAndProgress(frame, 0.35f);
                // 来源八方向常量过滤后的顺序，期望不调用正式排序入口。
                var basis = direction switch { 0 => new[] { 0, 1, 3, 2 }, 4 => new[] { 3, 2, 0, 1 }, _ => new[] { 3, 0, 1, 2 } };
                int Layer(int part)
                {
                    var key = $"{(int)gender}/{(int)resolved}/{part}/{sourceDirection}/{frame}";
                    return layers.TryGetProperty(key, out var value) ? value.GetInt32() : 0;
                }
                for (var part = 0; part < 4; part++)
                    Require(animations.Composition.Layer(gender, resolved, part, sourceDirection, frame) == Layer(part),
                        "导入的完整性别/动作层次与独立来源不符");
                var expected = basis.OrderBy(Layer).Select(part => partNames[part]);
                var facing = actor.GetNode<Node2D>("Facing");
                Require(facing.GetChildren().Select(node => node.Name.ToString()).SequenceEqual(expected),
                    $"来源排序不符：{gender}/{main}/{off}/{resolved}/{direction}/{frame}");
                foreach (var isMain in new[] { true, false })
                {
                    var id = isMain || main is 1017 or 1018 ? main : off;
                    var hand = actor.GetNode<Node2D>(isMain ? "Facing/RightHand" : "Facing/LeftHand");
                    var sprite = hand.GetNode<Sprite2D>("Weapon");
                    Require(sprite.ZIndex == 0 && hand.ZIndex == 0, "角色内层次改变世界排序");
                    if (id == 0) { Require(!sprite.Visible && sprite.Texture is null, "空槽残留纹理"); continue; }
                    var weapon = animations.GetWeapon(id, gender, resolved, isMain);
                    var native = id switch { 1012 or 1013 => 5, 1015 or 1016 => 6, 1017 or 1018 => 7, _ => 1 };
                    var action = (int)resolved + 1;
                    if (id == 1014 || action is 4 or 13 || action is >= 5 and <= 7 && action != native ||
                        action is >= 9 and <= 11 && action != native + 4) action = 1;
                    Require(weapon.SourceAction == action && weapon.SourceRightHand == isMain, "实际来源动作或源手不符");
                    var weaponCount = weapon.Frames.Length / 5;
                    var index = sourceDirection * weaponCount + frame % weaponCount;
                    Require(ReferenceEquals(sprite.Texture, weapon.Frames[index]), "武器未按身体帧取余采样");
                    var points = isMain ? resource.RightHandAttachments : resource.LeftHandAttachments;
                    Require(hand.Position == points[sourceDirection * count + frame] - resource.Anchor &&
                        sprite.Position == -weapon.Anchors[index], "源挂点或武器连接点漂移");
                }
                actor.Present(state with { Position = Vector2.One });
                Require(body.Frame == frame && Math.Abs(body.FrameProgress - 0.35f) < 0.001f, "位置刷新重启动作");
                total++;
            }
        }
        var run = new DataActorState(1, RoleGender.Male, Vector2.Zero, RoleMotion.Run, Vector2.One, 0, 1, 1012);
        actor.Present(run);
        body.SetFrameAndProgress(3, 0.4f);
        actor.Present(run with { MainHandId = 1015 });
        Require(body.Frame == 3 && Math.Abs(body.FrameProgress - 0.4f) < 0.001f, "同类身体动作换装丢失进度");
        actor.Present(run with { Motion = RoleMotion.Attack1 });
        body.SetFrameAndProgress(3, 0);
        actor.Present(run with { Motion = RoleMotion.Attack1, MainHandId = 1015 });
        Require(body.Frame == 0 && ReferenceEquals(body.SpriteFrames, animations.Get(RoleGender.Male, RoleMotion.Attack2).Frames),
            "更换武器未切换原生攻击身体");
        foreach (var next in new[] { 1012, 0 })
        {
            actor.Present(run with { MainHandId = 1017 });
            actor.Present(run with { MainHandId = next });
            var left = actor.GetNode<Sprite2D>("Facing/LeftHand/Weapon");
            Require(!left.Visible && left.Texture is null, "成对武器卸下或换剑残留左手图像");
        }
        GD.Print($"Source composition contracts passed: 9 combinations, 2 genders, 13 requests, 8 directions, {total} frames.");
    }

    /// <summary>报告明确的来源合成回归。</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
