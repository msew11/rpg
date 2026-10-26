using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Rpg.Config;
using Rpg.Const;
using Rpg.Services;
using Rpg.Utils;
using Rpg.World;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>按 client-actor-presentation 完整审查契约，直接导出正式角色节点的全部允许装备与身体帧。</summary>
public partial class ActorReviewExport : Node
{
    /// <summary>与八方向审查标签一致的屏幕向量，不通过生产方向映射生成测试输入。</summary>
    private static readonly Vector2[] Directions = [Vector2.Down, new(-1, 1), Vector2.Left, new(-1, -1),
        Vector2.Up, new(1, -1), Vector2.Right, new(1, 1)];
    /// <summary>每个方向的稳定文件名及中文标签。</summary>
    private static readonly string[] DirectionKeys = ["down", "down-left", "left", "up-left", "up", "up-right", "right", "down-right"];
    /// <summary>所有动作的中文审查名称，顺序与稳定动作枚举一致。</summary>
    private static readonly string[] MotionNames = ["站立", "跑步", "坐下", "死亡", "攻击1", "攻击2", "攻击3", "施法", "准备1", "准备2", "准备3", "受伤", "骑乘"];
    /// <summary>已读取纹理的可见像素范围，避免跨装备重复读取相同身体及武器帧。</summary>
    private readonly Dictionary<ulong, Rect2I> _usedRects = new();

    /// <summary>在隔离服务目录中测量画布、逐组实绘并写出完整清单，失败以退出码报告。</summary>
    public override async void _Ready()
    {
        var temporary = Path.Combine(Path.GetTempPath(), "rpg-actor-review-" + Guid.NewGuid());
        try
        {
            var arguments = OS.GetCmdlineUserArgs();
            var output = arguments.Single(value => value.StartsWith("--output="))[9..];
            var revision = arguments.Single(value => value.StartsWith("--revision="))[11..];
            if (!Path.IsPathFullyQualified(output)) throw new InvalidOperationException("导出目录必须是绝对路径");
            if (Directory.Exists(output)) throw new InvalidOperationException("导出目录已存在，拒绝覆盖");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, ".gdignore"), "");
            using var services = GameServicesTestFactory.Register(new EnvService(Path.Combine(temporary, "settings"), Path.Combine(temporary, "server")));
            var items = GameServices.Config.Item.Items.Values.ToArray();
            var mainIds = items.Where(item => EquipmentSlotUtil.CanEquip(item.Category, item.SubCategory, EquipmentSlot.MainHand))
                .Select(item => item.Id).Order().Prepend(0).ToArray();
            var offIds = items.Where(item => EquipmentSlotUtil.CanEquip(item.Category, item.SubCategory, EquipmentSlot.OffHand))
                .Select(item => item.Id).Order().Prepend(0).ToArray();
            var equipment = (from main in mainIds from off in offIds select (Main: main, Off: off)).ToArray();
            var probe = new ActorView();
            AddChild(probe);
            var bounds = MeasureAll(probe, equipment);
            probe.Free();
            const int padding = 16;
            var origin = new Vector2(padding - Mathf.Floor(bounds.Position.X), padding - Mathf.Floor(bounds.Position.Y));
            var cell = new Vector2I((Mathf.CeilToInt(bounds.Size.X) + padding * 2 + 7) / 8 * 8,
                (Mathf.CeilToInt(bounds.Size.Y) + padding * 2 + 7) / 8 * 8);
            GD.Print($"Review canvas: {cell}, anchor {origin}, {equipment.Length} equipment combinations.");
            var groups = new List<object>();
            var frameTotal = 0;
            foreach (var (main, off) in equipment)
            {
                foreach (var gender in Enum.GetValues<RoleGender>())
                foreach (var motion in Enum.GetValues<RoleMotion>())
                {
                    var (entry, count) = await CaptureGroup(output, main, off, gender, motion, cell, origin);
                    groups.Add(entry);
                    frameTotal += count;
                }
                GD.Print($"Review exported {groups.Count} groups / {frameTotal} frames: main={main}, off={off}.");
            }
            var manifest = new
            {
                schemaVersion = 1, generatedAt = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("O"),
                revision, renderer = RenderingServer.GetCurrentRenderingMethod(), headId = 1,
                cell = new { width = cell.X, height = cell.Y, anchorX = origin.X, anchorY = origin.Y, padding },
                directions = DirectionKeys.Select((key, index) => new { key, index,
                    label = new[] { "↓ 正面", "↙ 左下", "← 左侧", "↖ 左上", "↑ 背面", "↗ 右上", "→ 右侧", "↘ 右下" }[index] }),
                equipment = equipment.Select(pair => new { key = EquipmentKey(pair.Main, pair.Off), main = pair.Main, off = pair.Off,
                    name = $"主手：{ItemName(pair.Main)} / 副手：{ItemName(pair.Off)}" }),
                motions = Enum.GetValues<RoleMotion>().Select(motion => new { key = motion.ToString().ToLowerInvariant(), label = MotionNames[(int)motion] }),
                genders = new[] { new { key = "male", label = "男" }, new { key = "female", label = "女" } },
                groupCount = groups.Count, frameCount = frameTotal, groups
            };
            File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"Actor review export passed: {groups.Count} groups, {frameTotal} frames. Output: {output}");
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

    /// <summary>遍历全部配置允许组合，用实际可见像素确定统一画布，任何方向均不靠猜测裁切。</summary>
    private Rect2 MeasureAll(ActorView actor, (int Main, int Off)[] equipment)
    {
        Rect2? bounds = null;
        foreach (var (main, off) in equipment)
        foreach (var gender in Enum.GetValues<RoleGender>())
        foreach (var motion in Enum.GetValues<RoleMotion>())
        for (var direction = 0; direction < Directions.Length; direction++)
        {
            actor.Present(State(main, off, gender, motion, direction, Vector2.Zero));
            var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
            body.Pause();
            var count = body.SpriteFrames.GetFrameCount(body.Animation);
            for (var frame = 0; frame < count; frame++)
            {
                body.SetFrameAndProgress(frame, 0);
                var visible = VisibleBounds(actor);
                bounds = bounds?.Merge(visible) ?? visible;
            }
        }
        return bounds ?? throw new InvalidOperationException("没有可导出的角色帧");
    }

    /// <summary>将同一装备、性别与动作的八方向全部身体帧放进透明视口，保留正式绘制层次。</summary>
    private async Task<(object Entry, int Count)> CaptureGroup(string output, int main, int off, RoleGender gender,
        RoleMotion motion, Vector2I cell, Vector2 origin)
    {
        var resource = GameServices.Animation.Get(gender, motion);
        var count = resource.Frames.GetFrameCount("dir0");
        if (Enumerable.Range(0, 5).Any(index => resource.Frames.GetFrameCount($"dir{index}") != count))
            throw new InvalidOperationException($"身体方向帧数不一致：{gender}/{motion}");
        var viewport = new SubViewport { Size = new Vector2I(cell.X * count, cell.Y * 8), Disable3D = true,
            TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var samples = new List<object>();
        for (var direction = 0; direction < 8; direction++)
        {
            var handSamples = new List<object>();
            for (var frame = 0; frame < count; frame++)
            {
                var position = origin + new Vector2(frame * cell.X, direction * cell.Y);
                var actor = new ActorView();
                viewport.AddChild(actor);
                actor.Present(State(main, off, gender, motion, direction, position));
                var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
                body.Pause();
                body.SetFrameAndProgress(frame, 0);
                var visible = VisibleBounds(actor);
                var slot = new Rect2(new Vector2(frame * cell.X + 1, direction * cell.Y + 1), (Vector2)cell - Vector2.One * 2);
                if (!slot.Encloses(visible)) throw new InvalidOperationException($"帧超出画布：{gender}/{motion}/{direction}/{frame}/{visible}");
                handSamples.Add(new { frame, duration = resource.Frames.GetFrameDuration(body.Animation, frame),
                    main = WeaponSample(actor, main, true, gender, motion, direction),
                    off = WeaponSample(actor, off, false, gender, motion, direction) });
            }
            samples.Add(new { direction, frames = handSamples });
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = viewport.GetTexture().GetImage();
        var key = $"{gender.ToString().ToLowerInvariant()}/{EquipmentKey(main, off)}/{motion.ToString().ToLowerInvariant()}";
        var relative = $"sheets/{key}.png";
        var path = Path.Combine(output, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (image.GetPixel(0, 0).A != 0 || image.SavePng(path) != Error.Ok)
            throw new InvalidOperationException($"透明联系表保存失败：{path}");
        viewport.Free();
        return (new { key, gender = gender.ToString().ToLowerInvariant(), equipment = EquipmentKey(main, off),
            main, off, motion = motion.ToString().ToLowerInvariant(), count, fps = resource.Frames.GetAnimationSpeed("dir0"),
            loop = resource.Frames.GetAnimationLoopMode("dir0").ToString(), sheet = relative, samples }, count * 8);
    }

    /// <summary>记录实际左右手所选资源与采样帧，明确当前复用及镜像选择，不另行推测持握。</summary>
    private static object? WeaponSample(ActorView actor, int itemId, bool main, RoleGender gender, RoleMotion motion, int direction)
    {
        if (itemId == 0) return null;
        var item = GameServices.Config.Item.Items[itemId];
        var appearance = GameServices.Config.Equipment.Items[item.RelatedId].AppearanceId;
        var (_, mirrored) = AnimationService.ResolveDirection(IsometricProjection.ToLogical(Directions[direction]));
        var weapon = GameServices.Animation.GetWeapon(appearance, gender, motion, main != mirrored);
        var sprite = actor.GetNode<Sprite2D>(main ? "Facing/RightHand/Weapon" : "Facing/LeftHand/Weapon");
        var index = Array.IndexOf(weapon.Frames, sprite.Texture);
        if (index < 0) throw new InvalidOperationException("实绘武器未使用清单所记录的资源");
        return new { itemId, appearance, resource = weapon.ResourcePath, sourceRightHand = main != mirrored,
            nativeRightHand = weapon.SourceRightHand, crossHandFallback = (main != mirrored) != weapon.SourceRightHand,
            sourceIndex = index, sourceFrame = index % (weapon.Frames.Length / 5), framesPerDirection = weapon.Frames.Length / 5 };
    }

    /// <summary>合并身体与当前可见头部和装备的原始像素矩形，并应用节点的真实镜像变换。</summary>
    private Rect2 VisibleBounds(ActorView actor)
    {
        var body = actor.GetNode<AnimatedSprite2D>("Facing/Body");
        var texture = body.SpriteFrames.GetFrameTexture(body.Animation, body.Frame);
        var bounds = body.GlobalTransform * (Rect2)UsedRect(texture);
        foreach (var sprite in actor.FindChildren("*", "Sprite2D", true, false).OfType<Sprite2D>())
            if (sprite.Visible && sprite.Texture is { } frame)
            {
                var used = UsedRect(frame);
                // 补充握柄允许空帧，零面积矩形不能当作可见像素扩大审查画布。
                if (used.HasArea()) bounds = bounds.Merge(sprite.GlobalTransform * (Rect2)used);
            }
        return bounds;
    }

    /// <summary>缓存每张纹理的非透明范围，忽略原始素材画布的透明边缘。</summary>
    private Rect2I UsedRect(Texture2D texture)
    {
        if (_usedRects.TryGetValue(texture.GetInstanceId(), out var rect)) return rect;
        using var image = texture.GetImage();
        rect = image.GetUsedRect();
        _usedRects.Add(texture.GetInstanceId(), rect);
        return rect;
    }

    /// <summary>构造只用于实绘的快照，全部角色使用默认头型1且不访问正式会话或存档。</summary>
    private static DataActorState State(int main, int off, RoleGender gender, RoleMotion motion, int direction, Vector2 position) =>
        new(1, gender, IsometricProjection.ToLogical(position), motion, IsometricProjection.ToLogical(Directions[direction]), 0, 1, main, off);

    /// <summary>按两个槽位编号组成稳定目录名。</summary>
    private static string EquipmentKey(int main, int off) => $"main-{main}_off-{off}";

    /// <summary>显示配置中的真实道具名，空槽标为空手。</summary>
    private static string ItemName(int id) => id == 0 ? "空手" : GameServices.Config.Item.Items[id].Name;
}
