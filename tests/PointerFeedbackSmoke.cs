using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/client-ui 的 Normal 与文本鼠标、光圈扩散涟漪、点击隔离及世界生命周期。</summary>
public partial class PointerFeedbackSmoke : Node
{
    /// <summary>通过真实视口输入和帧推进验证点击反馈，不访问正式用户存档。</summary>
    public override async void _Ready()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rpg-pointer-" + Guid.NewGuid());
        DoubleMainController? main = null;
        try
        {
            Check(GD.Load<Texture2D>("res://assets/pointer/normal.png").GetSize() == new Vector2(41, 35), "Normal 画布不完整");
            var image = GD.Load<Texture2D>("res://assets/pointer/circle.png").GetImage();
            Check(image.GetSize() == new Vector2I(68, 41) && image.GetPixel(64, 20).A > 0, "circle 缺少右侧图块");
            main = new DoubleMainController { SettingsDirectory = directory, ServerDirectory = Path.Combine(directory, "server") };
            RoleTestData.Seed(main.ServerDirectory);
            AddChild(main);
            await Frames();
            var cursor = main.GetNode<UI.PointerCursor>("PointerCursor");
            var normal = cursor.CurrentTexture;
            Check(normal.GetSize() == new Vector2(51, 44), "指针未放大至约 125%");
            // 文本输入鼠标素材场景：真实悬停及编辑不得破坏密码或普通指针恢复。
            Check(GD.Load<Texture2D>("res://assets/pointer/edit.png").GetSize() == new Vector2(9, 15), "Edit 画布不完整");
            foreach (var edit in main.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>())
            {
                var pointInEdit = edit.GetGlobalRect().GetCenter();
                using (var motion = new InputEventMouseMotion { Position = pointInEdit, GlobalPosition = pointInEdit })
                    GetViewport().PushInput(motion, true);
                await Frames();
                Check(GetViewport().GuiGetHoveredControl() == edit && edit.GetCursorShape(edit.Size / 2) == Control.CursorShape.Ibeam,
                    "登录文本区未选择自定义 Ibeam 槽位");
                Click(pointInEdit);
                Check(edit.HasFocus(), "点击文本区未获得输入焦点");
                var original = edit.Text;
                edit.CaretColumn = edit.Text.Length;
                using (var key = new InputEventKey { Keycode = Key.X, Unicode = 'x', Pressed = true })
                    GetViewport().PushInput(key, true);
                Check(edit.Text == original + "x" && edit.Secret == (edit.PlaceholderText == "密码"), "文本或密码输入行为改变");
                edit.Text = original;
            }
            // 两个输入框连续划过场景：双向经过窄缝仍命中文本区域，点击窄缝不提交登录。
            var fields = (VBoxContainer)main.FindChild("LoginFields", true, false);
            var edits = fields.GetChildren().OfType<LineEdit>().ToArray();
            var accountRect = edits[0].GetGlobalRect();
            var passwordRect = edits[1].GetGlobalRect();
            var gapPoint = new Vector2(accountRect.GetCenter().X, (accountRect.End.Y + passwordRect.Position.Y) / 2);
            foreach (var pointInFields in new[] { accountRect.GetCenter(), gapPoint, passwordRect.GetCenter(), gapPoint, accountRect.GetCenter() })
            {
                using var motion = new InputEventMouseMotion { Position = pointInFields, GlobalPosition = pointInFields };
                GetViewport().PushInput(motion, true);
                var hovered = GetViewport().GuiGetHoveredControl();
                Check(hovered is not null && (hovered == fields || hovered.GetParent() == fields)
                    && hovered.GetCursorShape(pointInFields - hovered.GlobalPosition) == Control.CursorShape.Ibeam,
                    "账号和密码之间短暂切为普通手指");
            }
            Click(gapPoint);
            await Frames();
            Check(edits.All(edit => edit.Text == "demo") && !main.FindChildren("*", "Button", true, false)
                .OfType<Button>().Single(button => button.Text == "登录").Disabled, "窄缝点击改变文本或提交了登录");
            using (var motion = new InputEventMouseMotion { Position = new Vector2(10, 10), GlobalPosition = new Vector2(10, 10) })
                GetViewport().PushInput(motion, true);
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            Check(GetViewport().GuiGetHoveredControl() is not LineEdit && cursor.CurrentTexture == normal,
                "移出文本输入区域未恢复普通鼠标");
            using (var down = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = new Vector2(10, 10) })
                GetViewport().PushInput(down, true);
            var firstPressed = cursor.CurrentTexture;
            Check(cursor.IsPressed && firstPressed != normal, "按下未立即切换指针");
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            Check(cursor.IsPressed && cursor.CurrentTexture != normal && cursor.CurrentTexture != firstPressed &&
                cursor.CurrentTexture.GetSize() == normal.GetSize(), "按下动画未推进或尺寸跳变");
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            using (var up = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Position = new Vector2(10, 10) })
                GetViewport().PushInput(up, true);
            Check(!cursor.IsPressed && cursor.CurrentTexture == normal, "长按松开未立即恢复普通指针");
            Click(new Vector2(10, 10));
            Check(!cursor.IsPressed && cursor.CurrentTexture != normal, "同帧按下松开未保留反馈");
            await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            Check(cursor.CurrentTexture != normal && cursor.CurrentTexture != firstPressed, "短按没有显示第二帧");
            Click(new Vector2(10, 10));
            Check(cursor.CurrentTexture == firstPressed, "连续点击未从首帧重新播放");
            await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            Check(cursor.CurrentTexture == normal, "连续点击反馈结束后未恢复普通指针");
            Click(new Vector2(10, 10));
            Check(Circles(main).Length == 0, "登录页面创建了光圈");
            main.FindChildren("*", "Button", true, false).OfType<Button>().Single(button => button.Text == "登录")
                .EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Click(new Vector2(10, 10));
            Check(Circles(main).Length == 0, "选角页面创建了光圈");
            Press(main, "SelectRole1001");
            Press(main, "EnterRole");
            await Frames();
            var point = GetViewport().GetVisibleRect().Size / 2 + new Vector2(100, 70);
            Click(point, MouseButton.Right);
            Click(point, MouseButton.Left, false);
            Check(Circles(main).Length == 0, "右键或松开创建了光圈");

            var expected = GetViewport().GetCanvasTransform().AffineInverse() * point;
            Click(point);
            var first = Circles(main).Single();
            Check(first.GlobalPosition.IsEqualApprox(expected) && first.Centered && first.Scale == Vector2.One * 0.25f,
                "光圈未从小尺寸开始或没有中心对齐世界点击");
            Check(first.Texture.GetSize() == new Vector2(68, 41), "运行光圈未加载完整纹理");
            Click(point + new Vector2(80, 30));
            var circles = Circles(main);
            Check(circles.Length == 2 && circles[0].GlobalPosition != circles[1].GlobalPosition, "连续点击没有独立反馈");
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            Check(first.Modulate.A is > 0 and < 1 && first.GlobalPosition.IsEqualApprox(expected), "光圈未淡出或随摄像机漂移");
            var expandingScale = first.Scale.X;
            var fadingAlpha = first.Modulate.A;
            Check(expandingScale is > 0.25f and < 1.0f && Mathf.IsEqualApprox(first.Scale.Y, expandingScale),
                "光圈未放大或椭圆比例改变");
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            Check(first.Scale.X > expandingScale && first.Scale.X < 1.0f
                && Mathf.IsEqualApprox(first.Scale.Y, first.Scale.X) && first.Modulate.A < fadingAlpha
                && first.GlobalPosition.IsEqualApprox(expected), "涟漪没有继续扩散淡出或中心漂移");
            // 超过旧版 0.5 秒后仍有涟漪，验证 client-ui 的延长播放时长场景。
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            Check(GodotObject.IsInstanceValid(first) && first.Modulate.A > 0 && first.Scale.X is > 0.9f and <= 1.0f
                && Mathf.IsEqualApprox(first.Scale.Y, first.Scale.X), "涟漪未延长播放或最大尺寸不符合要求");
            await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
            Check(Circles(main).Length == 0 && !GodotObject.IsInstanceValid(first), "光圈播放后未释放");

            var target = GameServices.World.RoleTarget;
            var button = main.FindChildren("*", "Button", true, false).OfType<Button>().Single(node => node.Text == "GM");
            Click(button.GetGlobalRect().GetCenter());
            await Frames();
            Check(Circles(main).Length == 0 && GameServices.World.RoleTarget == target, "UI 点击穿透到地面");
            var panel = (Control)main.FindChild("GM", true, false);
            Check(panel.Visible, "真实视口点击未打开 GM");
            panel.Hide();
            Click(point);
            var pending = Circles(main).Single();
            // 操作系统菜单的真实返回按钮，覆盖光圈尚未结束的世界清理。
            var hud = main.FindChildren("*", "Control", true, false).OfType<UI.GameHud>().Single();
            hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(node => node.Text == "系统")
                .EmitSignal(Button.SignalName.Pressed);
            hud.GetNode("SystemMenuOverlay").FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(node => node.Text == "返回角色选择").EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(!GodotObject.IsInstanceValid(pending) && Circles(main).Length == 0 && !GameServices.World.HasWorld,
                "离开世界后残留点击光圈");
            GD.Print("Pointer feedback smoke passed: Normal, Edit login input, complete circle, expanding ripple, clicks, camera, fade, UI isolation, cleanup.");
            main.QueueFree();
            await Frames();
            main = null;
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            main?.QueueFree();
            GetTree().Quit(1);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    /// <summary>经由视口分派鼠标事件，覆盖真实 GUI 消费与未处理输入路径。</summary>
    private void Click(Vector2 position, MouseButton button = MouseButton.Left, bool pressed = true)
    {
        using var input = new InputEventMouseButton { Position = position, GlobalPosition = position,
            ButtonIndex = button, Pressed = pressed };
        GetViewport().PushInput(input, true);
        if (pressed) Click(position, button, false);
    }

    /// <summary>枚举实际仍属于当前世界的反馈节点。</summary>
    private static GroundClickView[] Circles(Node root) => root.FindChildren("*", "Sprite2D", true, false)
        .OfType<GroundClickView>().ToArray();

    /// <summary>操作真实业务按钮推进登录后的角色流程。</summary>
    private static void Press(Node root, string name) => ((Button)root.FindChild(name, true, false)).EmitSignal(Button.SignalName.Pressed);

    /// <summary>等待协议收发、布局和延迟释放完成。</summary>
    private async Task Frames()
    {
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>以异常和非零退出码报告验收失败。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
