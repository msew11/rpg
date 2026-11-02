using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Protocol;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.UI;
using Rpg.World.Utils;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/monster-selection：真实左右键、红剑、六帧圆环、像素命中、输入隔离及 AOI/世界清理。</summary>
public partial class MonsterSelectionSmoke : Node
{
    /// <summary>使用独立存档和正式协议建立怪物，验证选择只产生本地视觉状态。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-monster-selection-" + Guid.NewGuid());
        DoubleMainController? main = null;
        var subscriptions = new List<IDisposable>();
        try
        {
            RoleTestData.Seed(Path.Combine(directory, "server"));
            main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server"),
                EnvFilePath = Path.Combine(directory, ".env") };
            AddChild(main); await Frames();
            main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录").EmitSignal(Button.SignalName.Pressed);
            await Frames();
            ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            var view = (MonstersView)main.FindChild("MonstersView", true, false);
            var cursor = main.GetNode<PointerCursor>("PointerCursor");
            var camera = (Camera2D)main.FindChild("WorldCamera", true, false);
            var placement = (MonsterPlacementControl)main.FindChild("MonsterPlacement", true, false);
            var normal = cursor.CurrentTexture;
            var errors = new List<ErrorRt>();
            subscriptions.Add(GameServices.EventBus.Subscribe<ErrorRt>(errors.Add));
            var replies = new List<GmRt>();
            subscriptions.Add(GameServices.EventBus.Subscribe<GmRt>(replies.Add));
            var origin = GameServices.World.RolePosition;
            var first = await CreateMonster(view, new Vector2(670, 310));
            var second = await CreateMonster(view, new Vector2(870, 310));
            var firstId = Guid.Parse(first.Name.ToString()[8..]);
            var secondId = Guid.Parse(second.Name.ToString()[8..]);
            var ring = view.FindChild("MonsterSelectionRing", true, false) as AnimatedSprite2D;
            Check(ring is not null && !ring.Visible && ring.SpriteFrames.GetFrameCount("default") == 6, "圆环未完整导入六帧或默认可见");
            Check(ring!.SpriteFrames.GetAnimationLoopMode("default") == SpriteFrames.LoopMode.Linear &&
                ring.Offset == new Vector2(-34, -20) && ring.ShowBehindParent, "圆环循环、锚点或前后层次不符");
            Check(ring.SpriteFrames.GetFrameTexture("default", 0).GetSize() == new Vector2(69, 43), "圆环画布被裁切");
            var target = GameServices.World.RoleTarget;
            var point = PixelPoint(first, true);
            Motion(PixelPoint(first, false)); await Frames();
            Check(view.HoveredObjectId is null && !cursor.IsMonsterHovering, "透明像素触发怪物悬停");
            Motion(point); await Frames();
            Check(view.HoveredObjectId == firstId && cursor.IsMonsterHovering, "怪物悬停没有红色剑");
            var sword = cursor.CurrentTexture;
            Check(sword.GetSize() == new Vector2(50, 50) && cursor.CurrentHotspot == new Vector2(2.5f, 2.5f), "红剑尺寸或热点不符");
            using (var expected = (Image)GD.Load<Texture2D>("res://assets/pointer/sword.png").GetImage().Duplicate())
            {
                expected.Resize(50, 50, Image.Interpolation.Nearest);
                using var actual = (Image)sword.GetImage().Duplicate();
                Check(expected.GetData().SequenceEqual(actual.GetData()), "鼠标没有使用 AttackH 红剑原像素");
            }
            Click(point);
            Check(view.SelectedObjectId == firstId && ring.Visible && ring.GetParent() == first && cursor.CurrentTexture == sword,
                "右键没有立即选择或普通点击覆盖了红剑");
            Check(ring.Scale.IsEqualApprox(Vector2.One * (first.Texture.GetWidth() / 69f)), "圆环没有按目标宽度等比放大，可能被树根完全遮挡");
            await Frames();
            Check(GameServices.World.RoleTarget == target && GameServices.World.RolePosition == origin && !Circles(main), "选择同时触发移动或地面涟漪");
            var frames = new HashSet<int>();
            var looped = false;
            ring.AnimationLooped += () => looped = true;
            for (var index = 0; index < 50; index++)
            {
                frames.Add(ring.Frame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            Check(frames.Count == 6 && looped && ring.IsPlaying(), "圆环没有播放完整六帧并循环");
            var frameBefore = ring.Frame;
            Click(point);
            Check(ring.Frame == frameBefore, "重复点击重置圆环动画");
            // 左键攻击由 basic_combat_smoke 验收；本场景使用右键验证纯选择。
            Motion(PixelPoint(second, true)); await Frames(); Click(PixelPoint(second, true));
            Check(view.SelectedObjectId == secondId && ring.GetParent() == second &&
                view.FindChildren("MonsterSelectionRing", "AnimatedSprite2D", true, false).Count == 1, "没有切换唯一目标圆环");

            // UI、模态、失焦只阻止世界输入和悬停，不改变当前目标。
            point = PixelPoint(first, true);
            var layer = new CanvasLayer { Layer = 30 };
            var blocker = new Panel { Position = point - new Vector2(25, 25), Size = new Vector2(50, 50), MouseFilter = Control.MouseFilterEnum.Stop };
            layer.AddChild(blocker); main.AddChild(layer);
            Motion(point + Vector2.One); Motion(point); await Frames();
            Click(point); Click(point, MouseButton.Right); await Frames();
            Check(view.SelectedObjectId == secondId && !cursor.IsMonsterHovering && view.HoveredObjectId is null, "普通 UI 穿透选择或悬停");
            layer.QueueFree(); await Frames();
            var modal = new ConfirmationDialog { Exclusive = true }; main.AddChild(modal); modal.PopupCentered(); await Frames();
            Motion(point); await Frames();
            Check(!cursor.IsMonsterHovering && view.MonsterAt(point) is null && view.SelectedObjectId == secondId, "模态窗口未隔离怪物命中");
            modal.Hide(); modal.QueueFree(); await Frames();
            Motion(point); await Frames();
            view.Notification((int)NotificationApplicationFocusOut); cursor.Notification((int)NotificationApplicationFocusOut); await Frames();
            Check(view.HoveredObjectId is null && !cursor.IsMonsterHovering && view.SelectedObjectId == secondId, "失焦未清理悬停或错误清除选择");
            view.Notification((int)NotificationApplicationFocusIn); cursor.Notification((int)NotificationApplicationFocusIn);

            // 每种变换后由真实可见像素选择，圆环地面定位不受贴图 Offset 影响。
            foreach (var zoom in new[] { 0.5f, 1f, 2f, 1.75f })
            {
                camera.Zoom = Vector2.One * zoom; camera.Offset = new Vector2(90, -15); camera.ForceUpdateScroll();
                await Frames(); point = PixelPoint(first, true);
                Motion(point); await Frames(); Click(point);
                Check(view.SelectedObjectId == firstId && cursor.IsMonsterHovering, $"倍率 {zoom} 的怪物输入偏移");
                Check((ring.GetGlobalTransformWithCanvas() * Vector2.Zero).DistanceTo(first.GetGlobalTransformWithCanvas() * Vector2.Zero) < 0.01f,
                    "缩放平移后圆环偏离脚底锚点");
                Click(new Vector2(550, 160), MouseButton.Right);
                Check(view.SelectedObjectId is null && !ring.Visible, "右键空地未立即取消圆环");
            }
            camera.Zoom = Vector2.One; camera.Offset = Vector2.Zero; camera.ForceUpdateScroll(); await Frames();

            // 同位时后绘制的怪物优先命中。
            var third = await CreateMonster(view, first.GetGlobalTransformWithCanvas() * Vector2.Zero);
            var thirdId = Guid.Parse(third.Name.ToString()[8..]);
            point = PixelPoint(third, true); Motion(point); await Frames(); Click(point);
            Check(view.SelectedObjectId == thirdId, "重叠怪物没有优先选择后绘制目标");
            var replyCount = replies.Count;
            Check(GameServices.World.RoleTarget == target && GameServices.World.RolePosition == origin && errors.Count == 0,
                "选择交互改变移动或导致服务器错误");
            var chat = (ChatControl)main.FindChild("Chat", true, false);
            var input = (LineEdit)chat.FindChild("CommandInput", true, false);
            input.GrabFocus(); input.Text = "/monster"; input.CaretColumn = input.Text.Length;
            input.EmitSignal(LineEdit.SignalName.TextChanged, input.Text);
            using (var press = new InputEventKey { Keycode = Key.Enter, Pressed = true }) GetViewport().PushInput(press, true);
            using (var release = new InputEventKey { Keycode = Key.Enter }) GetViewport().PushInput(release, true);
            await Frames();
            Check(placement.Selected is not null && !view.InteractionEnabled, "放置模式未屏蔽怪物选择");
            Motion(point); await Frames();
            Check(!cursor.IsMonsterHovering, "放置模式仍显示红剑");
            Click(point, MouseButton.Right); await Frames();
            Check(placement.Selected is null && view.SelectedObjectId == thirdId && replies.Count == replyCount, "放置右键同时取消目标或发送了 GM");
            Motion(new Vector2(550, 160)); await Frames();
            Check(!cursor.IsMonsterHovering && cursor.CurrentTexture == normal, "移出怪物未恢复普通鼠标");
            Click(new Vector2(550, 160), MouseButton.Left); await Frames();
            Check(GameServices.World.RoleTarget != target && view.SelectedObjectId == thirdId, "左键空地未移动或错误取消目标");

            // 快速移到其他 AOI 块，通过正式服务端变化验证移出清理。
            GameServices.Session.SendGm("movespeed 5000"); await Frames();
            using (var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true })
                GameServices.World.HandleInput(click, origin + new Vector2(5000, 5000));
            await Until(() => !GameServices.World.MapObjects!.Objects.ContainsKey(thirdId));
            await Frames();
            Check(view.SelectedObjectId is null && !ring.Visible && view.HoveredObjectId is null, "目标移出 AOI 后仍保留圆环或选择");
            await Until(() => GameServices.Session.Role.Position.DistanceTo(origin + new Vector2(5000, 5000)) < 2 &&
                GameServices.World.RolePosition.DistanceTo(origin + new Vector2(5000, 5000)) < 2);
            await Frames();
            camera.ForceUpdateScroll();
            var fourth = await CreateMonster(view, new Vector2(750, 310));
            point = PixelPoint(fourth, true); Motion(point); await Frames();
            // 图形后端在等待帧内仍可能完成相机更新，点击使用事件当帧的实际可见像素。
            point = PixelPoint(fourth, true); Click(point);
            Check(view.SelectedObjectId is not null, "移动后无法重新选择怪物");
            if (OS.GetCmdlineUserArgs().Contains("--capture-monster-selection") && DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Directory.CreateDirectory("build");
                GetViewport().GetTexture().GetImage().SavePng("build/monster-selection.png");
            }
            main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "返回角色选择").EmitSignal(Button.SignalName.Pressed);
            Check(view.SelectedObjectId is null && !ring.Visible && view.HoveredObjectId is null, "离开世界没有同步清理目标");
            await Frames();
            ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
            ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
            await Frames();
            view = (MonstersView)main.FindChild("MonstersView", true, false);
            Check(view.SelectedObjectId is null && !cursor.IsMonsterHovering, "重入世界残留选中或红剑");
            Check(errors.Count == 0, "选择验收出现业务错误");
            foreach (var subscription in subscriptions) subscription.Dispose(); subscriptions.Clear();
            main.QueueFree(); await Frames(); main = null;
            GD.Print("Monster selection smoke passed: real left/right clicks, AttackH pixels/hotspot, all six looping frames, no move on selection, transparent/overlapping hits, zoom/pan, UI/modal/focus/placement isolation, ground movement, AOI removal and world reentry.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString()); GetTree().Quit(1);
        }
        finally
        {
            foreach (var subscription in subscriptions) subscription.Dispose();
            if (main is not null && IsInstanceValid(main)) main.Free();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    /// <summary>由实际相机逆投影生成怪物，等待正式 AOI 与表现节点。</summary>
    private async Task<Sprite2D> CreateMonster(MonstersView view, Vector2 viewportPoint)
    {
        var before = GameServices.World.MapObjects!.Objects.Keys.ToHashSet();
        var logical = IsometricProjection.ToLogical(view.GetCanvasTransform().AffineInverse() * viewportPoint);
        GameServices.Session.SendGm(FormattableString.Invariant($"monster 1001 {logical.X:R} {logical.Y:R}"));
        await Until(() => GameServices.World.MapObjects!.Objects.Keys.Any(id => !before.Contains(id)));
        var added = GameServices.World.MapObjects!.Objects.Keys.Single(id => !before.Contains(id));
        await Until(() => view.GetNodeOrNull<Sprite2D>("Monster_" + added.ToString("N")) is not null);
        return view.GetNode<Sprite2D>("Monster_" + added.ToString("N"));
    }

    /// <summary>从中心向外寻找真实像素或透明像素，避免硬编码怪物美术形状。</summary>
    private static Vector2 PixelPoint(Sprite2D sprite, bool opaque)
    {
        using var image = (Image)sprite.Texture.GetImage().Duplicate();
        var size = image.GetSize();
        var center = new Vector2(size.X, size.Y) / 2;
        for (var radius = 0; radius < Math.Max(size.X, size.Y); radius++)
        for (var y = Math.Max(0, (int)center.Y - radius); y < Math.Min(size.Y, (int)center.Y + radius + 1); y++)
        for (var x = Math.Max(0, (int)center.X - radius); x < Math.Min(size.X, (int)center.X + radius + 1); x++)
            if (opaque ? image.GetPixel(x, y).A > 0.5f : image.GetPixel(x, y).A == 0)
                return sprite.GetGlobalTransformWithCanvas() * (sprite.GetRect().Position + new Vector2(x + 0.5f, y + 0.5f));
        throw new InvalidOperationException("怪物测试素材缺少所需像素");
    }

    /// <summary>确认选择没有生成普通地面点击涟漪。</summary>
    private static bool Circles(Node main) => main.FindChildren("*", "Node", true, false).OfType<GroundClickView>().Any();

    /// <summary>注入真实鼠标移动，让 GUI 遮挡和硬件指针同时更新。</summary>
    private void Motion(Vector2 point)
    {
        using var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point };
        GetViewport().PushInput(motion, true);
    }

    /// <summary>使用完整的按下和松开事件验收世界输入。</summary>
    private void Click(Vector2 point, MouseButton button = MouseButton.Right)
    {
        Motion(point);
        using var press = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true };
        using var release = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button };
        GetViewport().PushInput(press, true); GetViewport().PushInput(release, true);
    }

    /// <summary>在有限时间内等待真实服务器调度与可见对象更新。</summary>
    private async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline) await Frames();
        Check(condition(), "等待怪物或 AOI 变化超时");
    }

    /// <summary>推进正式网络、物理、界面布局和延迟释放。</summary>
    private async Task Frames()
    {
        await SmokeNetwork.WaitForAccount(this);
        for (var index = 0; index < 5; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>断言失败即报告非零场景退出。</summary>
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
