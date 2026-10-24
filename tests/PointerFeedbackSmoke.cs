using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Rpg.Services;
using Rpg.Tests.Doubles;
using Rpg.World.Views;

namespace Rpg.Tests;

/// <summary>验收 openspec/specs/client-ui 的 Normal 指针、完整光圈、点击隔离及世界生命周期。</summary>
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
            AddChild(main);
            await Frames();
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
            Check(first.GlobalPosition.IsEqualApprox(expected) && first.Centered && first.Scale == Vector2.One,
                "光圈没有按原始尺寸中心对齐世界点击");
            Check(first.Texture.GetSize() == new Vector2(68, 41), "运行光圈未加载完整纹理");
            Click(point + new Vector2(80, 30));
            var circles = Circles(main);
            Check(circles.Length == 2 && circles[0].GlobalPosition != circles[1].GlobalPosition, "连续点击没有独立反馈");
            await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
            Check(first.Modulate.A is > 0 and < 1 && first.GlobalPosition.IsEqualApprox(expected), "光圈未淡出或随摄像机漂移");
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
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
            var hud = main.FindChildren("*", "Control", true, false).OfType<Rpg.UI.GameHud>().Single();
            hud.FindChildren("*", "Button", true, false).OfType<Button>().Single(node => node.Text == "系统")
                .EmitSignal(Button.SignalName.Pressed);
            hud.GetNode("SystemMenuOverlay").FindChildren("*", "Button", true, false).OfType<Button>()
                .Single(node => node.Text == "返回角色选择").EmitSignal(Button.SignalName.Pressed);
            await Frames();
            Check(!GodotObject.IsInstanceValid(pending) && Circles(main).Length == 0 && !GameServices.World.HasWorld,
                "离开世界后残留点击光圈");
            GD.Print("Pointer feedback smoke passed: Normal, complete circle, clicks, camera, fade, UI isolation, cleanup.");
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
