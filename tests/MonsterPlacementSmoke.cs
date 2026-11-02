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

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/gm-monster：真实键鼠、延迟发送、虚影与实物等大同位、UI 隔离及离开清理。</summary>
public partial class MonsterPlacementSmoke : Node
{
    /// <summary>从独立存档运行正式页面，失败以非零退出码报告。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-monster-ui-" + Guid.NewGuid());
        DoubleMainController? main = null;
        var subscriptions = new List<IDisposable>();
        try
        {
            RoleTestData.Seed(Path.Combine(directory, "server"));
            main = await Start(directory);
            var chat = (ChatControl)main.FindChild("Chat", true, false);
            var input = (LineEdit)chat.FindChild("CommandInput", true, false);
            var candidates = (ItemList)chat.FindChild("CommandCandidates", true, false);
            var panel = (Control)chat.FindChild("CommandSuggestions", true, false);
            var placement = (MonsterPlacementControl)main.FindChild("MonsterPlacement", true, false);
            var preview = placement.GetNode<Sprite2D>("MonsterPlacementPreview");
            var replies = new List<GmRt>(); var errors = new List<ErrorRt>();
            subscriptions.Add(GameServices.EventBus.Subscribe<GmRt>(replies.Add));
            subscriptions.Add(GameServices.EventBus.Subscribe<ErrorRt>(errors.Add));
            var target = GameServices.World.RoleTarget;

            SetInput(input, "/monster"); await Frames();
            Check(panel.Visible && candidates.ItemCount == 1 && candidates.GetItemText(0).Contains("1001") && candidates.GetItemText(0).Contains("树妖"), "列表未读取树妖配置");
            Click(candidates.GetGlobalRect().Position + new Vector2(25, 15)); await Frames();
            Check(placement.Selected is null && panel.Visible, "鼠标选择过早进入放置");
            KeyPress(Key.Enter); await Frames();
            Check(placement.Selected?.Id == 1001 && preview.Visible && preview.Texture is not null && !panel.Visible && !input.HasFocus(), "回车未进入树妖缩影");
            Check(replies.Count == 0 && errors.Count == 0 && GameServices.World.MapObjects!.Objects.Count == 0, "确认前发送了 GM");
            Check(preview.Scale == Vector2.One && preview.Offset == new Vector2(-69, -90) && preview.Modulate.A < 1, "虚影尺寸、透明度或锚点错误");

            Click(chat.GetGlobalRect().Position + new Vector2(20, 20)); await Frames();
            Check(placement.Selected is not null && replies.Count == 0 && errors.Count == 0 && GameServices.World.RoleTarget == target, "UI 点击发送或穿透地面");
            Click(chat.GetGlobalRect().Position + new Vector2(20, 20), MouseButton.Right); await Frames();
            Check(placement.Selected is null && !preview.Visible && replies.Count == 0 && errors.Count == 0, "UI 上右键没有无请求取消");

            SetInput(input, "/monster "); KeyPress(Key.KpEnter); await Frames();
            Check(placement.Selected is not null, "小键盘回车未确认怪物");
            var camera = (Camera2D)main.FindChild("WorldCamera", true, false);
            camera.Zoom = Vector2.One * 0.75f;
            camera.ForceUpdateScroll();
            // 将相机视野临时移到地图外，同一帧注入点击验证越界不会自动裁剪放置。
            var cameraPosition = camera.Position;
            camera.Position = new Vector2(-100000, -100000); camera.ForceUpdateScroll();
            Click(new Vector2(700, 280));
            camera.Position = cameraPosition; camera.ForceUpdateScroll();
            await Frames();
            Check(placement.Selected is not null && replies.Count == 0 && errors.Count == 0, "越界地面发送了创建请求");

            var point = new Vector2(730, 310);
            using (var motion = new InputEventMouseMotion { Position = point, GlobalPosition = point }) GetViewport().PushInput(motion, true);
            await Frames();
            Check((preview.GetGlobalTransformWithCanvas() * Vector2.Zero).DistanceTo(point) < 0.01f, "虚影地面锚点未跟随鼠标");
            var expected = IsometricProjection.ToLogical(GetViewport().GetCanvasTransform().AffineInverse() * point);
            var previewCorners = SpriteCorners(preview);
            var previewLogicalPosition = placement.LogicalPosition;
            Click(point);
            await Wait(() => replies.Count == 1 && GameServices.World.MapObjects?.Objects.Count == 1);
            Check(placement.Selected is null && !preview.Visible && errors.Count == 0 && GameServices.World.RoleTarget == target, "放置后状态或移动隔离错误");
            var monster = GameServices.World.MapObjects!.Objects.Values.Single();
            Check(monster.MonsterTypeId == "1001" && new Vector2(monster.X, monster.Y).DistanceTo(expected) < 0.01f, "缩放后的坐标与点击逆投影不一致");
            Check(replies[0].Command.StartsWith("monster 1001 ", StringComparison.Ordinal) && replies[0].Message.Contains("坐标"), "GM 没有最终坐标反馈");
            await Wait(() => main.FindChild("Monster_" + monster.ObjectId.ToString("N"), true, false) is Sprite2D);
            var sprite = (Sprite2D)main.FindChild("Monster_" + monster.ObjectId.ToString("N"), true, false);
            Check(sprite.Texture is not null && sprite.Position.DistanceTo(IsometricProjection.ToScreen(expected)) < 0.01f && sprite.Offset == new Vector2(-69, -90), "正式树妖贴图位置或锚点错误");
            Check(new Vector2(monster.X, monster.Y) == previewLogicalPosition, "请求落点没有精确使用虚影的逻辑坐标");
            CheckCorners(previewCorners, SpriteCorners(sprite), "首次放置后贴图与虚影不重合");

            // 比较真实渲染变换后的四角，覆盖缩小、原大、放大及两层缩放的最终倍率。
            foreach (var zoom in new[] { 0.5f, 1f, 2f, 1.25f * 1.4f })
            {
                SetInput(input, "/monster"); KeyPress(Key.Enter); await Frames();
                var pointer = new Vector2(810, 290);
                using (var motion = new InputEventMouseMotion { Position = pointer, GlobalPosition = pointer }) GetViewport().PushInput(motion, true);
                await Frames();
                camera.Zoom = Vector2.One * zoom;
                camera.Offset = new Vector2(37, -23);
                camera.ForceUpdateScroll();
                await Frames();
                Check((preview.GetGlobalTransformWithCanvas() * Vector2.Zero).DistanceTo(pointer) < 0.02f, "静止鼠标在平移或缩放后偏离虚影锚点");
                var corners = SpriteCorners(preview);
                var logical = placement.LogicalPosition;
                var beforeIds = GameServices.World.MapObjects!.Objects.Keys.ToHashSet();
                var previousReplies = replies.Count;
                Click(pointer);
                await Wait(() => replies.Count == previousReplies + 1 && GameServices.World.MapObjects!.Objects.Count == beforeIds.Count + 1);
                var added = GameServices.World.MapObjects!.Objects.Values.Single(value => !beforeIds.Contains(value.ObjectId));
                await Wait(() => main.FindChild("Monster_" + added.ObjectId.ToString("N"), true, false) is Sprite2D);
                var placed = (Sprite2D)main.FindChild("Monster_" + added.ObjectId.ToString("N"), true, false);
                Check(new Vector2(added.X, added.Y) == logical, "服务器对象坐标偏离虚影逻辑坐标");
                CheckCorners(corners, SpriteCorners(placed), $"倍率 {zoom} 放置后贴图四角与虚影不重合");
                Check(GameServices.World.RoleTarget == target && errors.Count == 0, "对齐验收触发移动或服务器错误");
            }
            var creationCount = replies.Count;

            SetInput(input, "/monster"); KeyPress(Key.Enter); await Frames();
            Click(new Vector2(800, 320), MouseButton.Right); await Frames();
            Check(placement.Selected is null && replies.Count == creationCount, "地面右键取消又创建了怪物");
            Click(new Vector2(760, 330)); await Frames();
            Check(GameServices.World.RoleTarget != target, "取消后普通地面点击没有恢复移动");
            SetInput(input, "/monster"); KeyPress(Key.Enter); await Frames();
            if (OS.GetCmdlineUserArgs().Contains("--capture-monster") && DisplayServer.GetName() != "headless")
            {
                // 自动截图将缩影摆在视口内，不依赖运行测试时操作系统鼠标停留的位置。
                placement.UpdatePointer(new Vector2(940, 300));
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Directory.CreateDirectory("build");
                GetViewport().GetTexture().GetImage().SavePng("build/monster-placement.png");
            }
            // 返回选角必须同步清理本地状态，不能在延迟释放前再发送。
            main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "返回角色选择").EmitSignal(Button.SignalName.Pressed);
            Check(placement.Selected is null && !preview.Visible, "离开世界未同步清理缩影");
            await Frames();
            Check(replies.Count == creationCount, "离开世界补发了怪物请求");
            foreach (var subscription in subscriptions) subscription.Dispose(); subscriptions.Clear();
            main.QueueFree(); await Frames(); main = null;

            main = await Start(directory);
            Check(GameServices.World.MapObjects!.Objects.Count == creationCount && GameServices.World.MapObjects.Objects.ContainsKey(monster.ObjectId), "场景重建后树妖未恢复");
            chat = (ChatControl)main.FindChild("Chat", true, false);
            input = (LineEdit)chat.FindChild("CommandInput", true, false);
            SetInput(input, "/monster"); KeyPress(Key.Enter); await Frames();
            placement = (MonsterPlacementControl)main.FindChild("MonsterPlacement", true, false);
            preview = placement.GetNode<Sprite2D>("MonsterPlacementPreview");
            GameServices.EventBus.Publish(new SessionKickedNotice { Message = "验收连接控制失效" });
            Check(placement.Selected is null && !preview.Visible, "连接控制失效未立即取消缩影");
            await Frames();
            main.QueueFree(); await Frames(); main = null;
            Check(!GameServices.IsInitialized, "离树后服务仍残留");
            GD.Print("Monster placement smoke passed: config list, Enter/keypad, zero early GM, UI/invalid-point isolation, equal preview/placed corners at 0.5/1/2/1.75 zoom with camera pan, exact logical coordinates, right cancel, leave and restart.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
        finally
        {
            foreach (var subscription in subscriptions) subscription.Dispose();
            if (main is not null && IsInstanceValid(main)) main.Free();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    /// <summary>取得贴图矩形四角经过世界相机后的视口坐标，用于比较实际尺寸与位置。</summary>
    private static Vector2[] SpriteCorners(Sprite2D sprite)
    {
        var rect = sprite.GetRect();
        var transform = sprite.GetGlobalTransformWithCanvas();
        return [transform * rect.Position, transform * new Vector2(rect.End.X, rect.Position.Y),
            transform * rect.End, transform * new Vector2(rect.Position.X, rect.End.Y)];
    }

    /// <summary>逐角对比可见贴图的几何位置，允许小于一个像素的浮点投影误差。</summary>
    private static void CheckCorners(Vector2[] expected, Vector2[] actual, string message)
    {
        for (var index = 0; index < 4; index++) Check(expected[index].DistanceTo(actual[index]) < 0.02f, message);
    }

    /// <summary>通过真实登录与选角按钮进入隔离世界。</summary>
    private async Task<DoubleMainController> Start(string directory)
    {
        var main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server"), EnvFilePath = Path.Combine(directory, ".env") };
        AddChild(main); await Frames();
        main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录").EmitSignal(Button.SignalName.Pressed);
        await Frames();
        ((Button)main.FindChild("SelectRole1001", true, false)).EmitSignal(Button.SignalName.Pressed);
        ((Button)main.FindChild("EnterRole", true, false)).EmitSignal(Button.SignalName.Pressed);
        await Frames();
        return main;
    }

    /// <summary>模拟文字编辑，按真实文本变更信号刷新候选。</summary>
    private static void SetInput(LineEdit input, string text)
    {
        input.GrabFocus(); input.Text = text; input.CaretColumn = text.Length;
        input.EmitSignal(LineEdit.SignalName.TextChanged, text);
    }

    /// <summary>注入完整按下松开，避免只调用界面回调。</summary>
    private void KeyPress(Key key)
    {
        using var press = new InputEventKey { Keycode = key, Pressed = true };
        using var release = new InputEventKey { Keycode = key, Pressed = false };
        GetViewport().PushInput(press, true); GetViewport().PushInput(release, true);
    }

    /// <summary>注入左右键真实视口点击。</summary>
    private void Click(Vector2 point, MouseButton button = MouseButton.Left)
    {
        using var press = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = true };
        using var release = new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = button, Pressed = false };
        GetViewport().PushInput(press, true); GetViewport().PushInput(release, true);
    }

    /// <summary>在有限时间内等待正式网络与 AOI 推进。</summary>
    private async Task Wait(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline) await Frames();
        Check(condition(), "等待怪物 GM 或 AOI 超时");
    }

    /// <summary>让网络、GUI 布局和延迟释放经过真实帧。</summary>
    private async Task Frames()
    {
        await SmokeNetwork.WaitForAccount(this);
        for (var index = 0; index < 5; index++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>失败直接中止当前验收。</summary>
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
