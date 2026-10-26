using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Rpg.Const;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>client-actor-presentation：真实GPU验证跨手剑柄连接、手掌覆盖与近手剑身覆盖腿部。</summary>
internal static class ActorSwordGripAssertions
{
    /// <summary>实绘男女站立和跑步八方向全部帧，逐像素验证用户指出的右上、右下回归。</summary>
    public static async Task VerifyRendered(Node host, Vector2[] directions)
    {
        var viewport = new SubViewport { Size = new Vector2I(1440, 800), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        host.AddChild(viewport);
        viewport.AddChild(new ColorRect { Size = viewport.Size, Color = new Color("24303d") });
        var actors = new List<ActorView>();
        for (var row = 0; row < 4; row++)
        for (var direction = 0; direction < 8; direction++)
        {
            var actor = new ActorView();
            viewport.AddChild(actor);
            actor.Present(new DataActorState(1, (RoleGender)(row % 2),
                IsometricProjection.ToLogical(new Vector2(90 + direction * 180, 180 + row * 195)),
                row < 2 ? RoleMotion.Stand : RoleMotion.Run, IsometricProjection.ToLogical(directions[direction]), 0, 1, 1012));
            actor.GetNode<AnimatedSprite2D>("Facing/Body").Pause();
            actors.Add(actor);
        }
        var bridges = new int[2];
        var palms = new int[2];
        var legs = new int[2];
        var contacts = new int[2];
        for (var step = 0; step < 8; step++)
        {
            // 先验收用户明确指出的第6帧，再逐帧检查其余姿态，不能用整组接触数代替。
            var frame = (step + 6) % 8;
            foreach (var actor in actors)
            {
                var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                body.SetFrameAndProgress(frame % body.SpriteFrames.GetFrameCount(body.Animation), 0);
            }
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var rendered = viewport.GetTexture().GetImage();
            for (var row = 0; row < 4; row++)
            foreach (var direction in new[] { 5, 7 })
            {
                var actor = actors[row * 8 + direction];
                var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                var sword = actor.GetNode<Sprite2D>("Facing/RightHand/Weapon");
                var grip = actor.GetNode<Sprite2D>("Facing/RightWeaponGrip");
                if (row == 2 && direction == 5) VerifyPalmSeam(actor, rendered, frame);
                using var bodyImage = body.SpriteFrames.GetFrameTexture(body.Animation, body.Frame).GetImage();
                using var swordImage = sword.Texture.GetImage();
                using var gripImage = grip.Texture.GetImage();
                for (var y = 0; y < swordImage.GetHeight(); y++)
                for (var x = 0; x < swordImage.GetWidth(); x++)
                {
                    var pixel = new Vector2(x + 0.5f, y + 0.5f);
                    var world = sword.ToGlobal(pixel);
                    var bodyPoint = (Vector2I)body.ToLocal(world).Floor();
                    var bodyColor = bodyPoint.X >= 0 && bodyPoint.Y >= 0 && bodyPoint.X < bodyImage.GetWidth() && bodyPoint.Y < bodyImage.GetHeight()
                        ? bodyImage.GetPixelv(bodyPoint) : Colors.Transparent;
                    var swordColor = swordImage.GetPixel(x, y);
                    var gripColor = gripImage.GetPixel(x, y);
                    var actual = rendered.GetPixelv((Vector2I)world.Floor());
                    var hand = actor.GetNode<Node2D>("Facing/RightHand");
                    if (direction == 5 && world.DistanceSquaredTo(hand.GlobalPosition) <= 25 && swordColor.A == 1 && bodyColor.A == 1)
                        contacts[row % 2]++;
                    if (direction == 7 && row >= 2 && world.Y >= actor.Position.Y - 25 &&
                        swordColor.A == 1 && bodyColor.A == 1 && swordColor.ToHtml() != bodyColor.ToHtml())
                    {
                        Require(actual.ToHtml() == swordColor.ToHtml(), $"右下近手剑身被腿截断：{row}/{frame}/{x},{y}");
                        legs[row % 2]++;
                    }
                    if (direction != 5 || gripColor.A != 1 || swordColor.A != 0) continue;
                    if (bodyColor.A == 0)
                    {
                        Require(actual.ToHtml() == gripColor.ToHtml(), $"右上握柄缺口未连接：{row}/{frame}/{x},{y}");
                        bridges[row % 2]++;
                    }
                    else if (bodyColor.A == 1)
                    {
                        Require(actual.ToHtml() == bodyColor.ToHtml(), "补充握柄覆盖了身体手形");
                        palms[row % 2]++;
                    }
                }
            }
            Require(rendered.SavePng(ProjectSettings.GlobalizePath($"res://build/sword-grip-preview-{frame}.png")) == Error.Ok,
                "长剑持握实绘保存失败");
        }
        viewport.Free();
        // 男性右上原画存在断柄，女性同方向原柄完整；两性都必须有握柄接触与腿部交叠样本。
        for (var gender = 0; gender < 2; gender++)
            Require(contacts[gender] > 0 && legs[gender] > 0 && (gender != 0 || bridges[gender] > 0 && palms[gender] > 0),
                $"缺少有效交叠样本：{gender}/握柄{bridges[gender]}/手掌{palms[gender]}/腿{legs[gender]}");
        GD.Print($"Sword grip GPU passed: visible bridges {string.Join(',', bridges)}, body masks {string.Join(',', palms)}, native grip contacts {string.Join(',', contacts)}, blade/leg overlaps {string.Join(',', legs)}.");
    }

    /// <summary>按人工审查的八帧上侧接缝坐标独立验收，任何一帧露背景均失败。</summary>
    private static void VerifyPalmSeam(ActorView actor, Image rendered, int frame)
    {
        var hand = actor.GetNode<Node2D>("Facing/RightHand");
        // 坐标取自手掌外缘到护手的接缝，使用镜像前局部坐标；不由连接层反推期望值。
        var seams = new[] { new Vector2(-4, -1), new Vector2(-4, 0), new Vector2(-3, -2),
            new Vector2(-3, -2), new Vector2(-2, -4), new Vector2(-4, -1), new Vector2(-3, -3), new Vector2(-3, -2) };
        var world = hand.ToGlobal(seams[frame] + new Vector2(0.5f, 0.5f));
        var actual = rendered.GetPixelv((Vector2I)world.Floor());
        Require(actual.ToHtml() != new Color("24303d").ToHtml(), $"男性右上跑步第{frame}帧手掌与护手之间仍露背景");
        // 第6帧上侧有连续两列接缝，必须全部连接，单个角点接触不足以视为握住。
        if (frame != 6) return;
        foreach (var pixel in new[] { new Vector2(-2, -3), new Vector2(-4, -2), new Vector2(-3, -2), new Vector2(-4, -1) })
        {
            world = hand.ToGlobal(pixel + new Vector2(0.5f, 0.5f));
            Require(rendered.GetPixelv((Vector2I)world.Floor()).ToHtml() != new Color("24303d").ToHtml(),
                $"男性右上跑步第6帧上侧接缝不完整：{pixel}");
        }
    }

    /// <summary>实际像素不符合遮挡契约时终止GPU冒烟并报告位置。</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
