using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using Rpg.Const;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>client-actor-presentation：以独立原始数据核验全部装备，防止镜像时错用另一只手的原画。</summary>
internal static class ActorWeaponHandAssertions
{
    /// <summary>验证七件装备及双手变体的全部源帧，并覆盖飞刀男女八方向跑步每帧。</summary>
    public static void Verify(AnimationService animations, ActorView actor)
    {
        using var golden = JsonDocument.Parse(FileAccess.GetFileAsString("res://tests/fixtures/actor_weapon_sources.json"));
        var paths = new HashSet<string>();
        var total = 0;
        foreach (var entry in golden.RootElement.GetProperty("resources").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            Require(paths.Add(path), "原画黄金记录重复");
            var weapon = GD.Load<DataActorWeapon>(path);
            Require(weapon.Frames.Length == entry.GetProperty("frames").GetInt32(), $"原画帧数改变：{path}");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for (var frame = 0; frame < weapon.Frames.Length; frame++)
            {
                using var image = weapon.Frames[frame].GetImage();
                image.Convert(Image.Format.Rgba8);
                var pixels = image.GetData();
                if (weapon.RearFrames.Length != 0)
                {
                    using var rear = weapon.RearFrames[frame].GetImage();
                    rear.Convert(Image.Format.Rgba8);
                    var rearPixels = rear.GetData();
                    for (var offset = 0; offset < pixels.Length; offset += 4)
                    {
                        Require(pixels[offset + 3] == 0 || rearPixels[offset + 3] == 0, "长柄两段像素重复");
                        if (rearPixels[offset + 3] != 0) Array.Copy(rearPixels, offset, pixels, offset, 4);
                    }
                }
                for (var offset = 0; offset < pixels.Length; offset += 4)
                    if (pixels[offset + 3] < 255) Array.Clear(pixels, offset, 3);
                hash.AppendData(pixels);
                var metadata = new byte[20];
                var values = new[] { (int)weapon.Anchors[frame].X, (int)weapon.Anchors[frame].Y,
                    weapon.Layers[frame], image.GetWidth(), image.GetHeight() };
                for (var index = 0; index < values.Length; index++)
                    BinaryPrimitives.WriteInt32LittleEndian(metadata.AsSpan(index * 4, 4), values[index]);
                hash.AppendData(metadata);
            }
            Require(Convert.ToHexString(hash.GetHashAndReset()) == entry.GetProperty("sha256").GetString(),
                $"原始像素、连接点或层次不符：{path}");
            total += weapon.Frames.Length;
        }
        var used = new HashSet<string>();
        foreach (var id in Enumerable.Range(1012, 7))
        foreach (var gender in Enum.GetValues<RoleGender>())
        foreach (var motion in Enum.GetValues<RoleMotion>())
        foreach (var right in new[] { true, false })
            used.Add(animations.GetWeapon(id, gender, motion, right).ResourcePath);
        Require(paths.SetEquals(used), "装备基础原画或左右手变体未被黄金记录完整覆盖");

        var directions = new[] { Vector2.Down, new Vector2(-1, 1), Vector2.Left, new Vector2(-1, -1),
            Vector2.Up, new Vector2(1, -1), Vector2.Right, new Vector2(1, 1) };
        foreach (var gender in Enum.GetValues<RoleGender>())
        for (var direction = 0; direction < directions.Length; direction++)
        {
            actor.Present(new DataActorState(1, gender, Vector2.Zero, RoleMotion.Run,
                IsometricProjection.ToLogical(directions[direction]), 0, 1, 0, 1018));
            var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
            var sprite = actor.GetNode<Sprite2D>("Facing/LeftHand/Weapon");
            // 独立写出三个镜像方向，避免调用生产映射生成期望值。
            var sourceDirection = new[] { 4, 0, 1, 2, 3, 2, 1, 0 }[direction];
            var folder = direction >= 5 ? "right/" : "";
            var expected = GD.Load<DataActorWeapon>($"res://assets/characters/weapons/1018/{folder}{gender.ToString().ToLowerInvariant()}/run.tres");
            Require(expected.Frames.Length == 40, "飞刀跑步原画不是五方向各八帧");
            for (var frame = 0; frame < 8; frame++)
            {
                body.SetFrameAndProgress(frame, 0.35f);
                var index = sourceDirection * 8 + frame;
                Require(ReferenceEquals(sprite.Texture, expected.Frames[index]) && sprite.Position == -expected.Anchors[index],
                    $"飞刀镜像持握错手：{gender}/{direction}/{frame}");
            }
        }
        GD.Print($"Weapon source goldens passed: {paths.Count} resources / {total} frames, pixels/anchors/layers; throwing blade run both genders, all 8 directions/frames.");
    }

    /// <summary>出现回归时报告明确失败原因，交由冒烟宿主输出非零退出码。</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
